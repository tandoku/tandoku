namespace Tandoku.Markdown;

using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Tandoku.Content;
using Tandoku.Serialization;

public sealed partial class MarkdownExporter
{
    private const string MarkdownExtension = ".md";
    private const string DefaultBaseName = "content";
    private const string BlockTemplateResourceName = "Tandoku.Markdown.Templates.Block.scriban-md";

    private static readonly Lazy<Template> DefaultBlockTemplate = new(LoadDefaultBlockTemplate);

    private readonly IFileSystem fileSystem;
    private readonly MarkdownExportSettings settings;
    private readonly Template blockTemplate;

    public MarkdownExporter(MarkdownExportSettings? settings = null, IFileSystem? fileSystem = null)
    {
        this.settings = (settings ?? new MarkdownExportSettings()).ApplyQuirks();
        this.fileSystem = fileSystem ?? new FileSystem();
        this.blockTemplate = string.IsNullOrEmpty(this.settings.TemplatePath)
            ? DefaultBlockTemplate.Value
            : LoadBlockTemplateFromFile(this.fileSystem, this.settings.TemplatePath);
    }

    public async Task<IReadOnlyList<string>> ExportAsync(string inputPath, string outputPath)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var contentFiles = inputDir.EnumerateContentFiles().ToList();

        if (contentFiles.Count == 0)
            return [];

        var written = new List<string>();

        if (this.settings.Combine)
        {
            string combinedPath;
            string targetDirectory;
            if (this.fileSystem.Path.ExtensionEquals(outputPath, MarkdownExtension))
            {
                combinedPath = outputPath;
                targetDirectory = this.fileSystem.Path.GetDirectoryName(outputPath) ?? string.Empty;
            }
            else
            {
                targetDirectory = outputPath;
                // TODO - consider exposing the combined output filename as a property on volume info
                // (and possibly drop the moniker, just using the cleaned title)
                combinedPath = this.fileSystem.Path.Combine(outputPath, DefaultBaseName + MarkdownExtension);
            }

            if (!string.IsNullOrEmpty(targetDirectory))
                this.fileSystem.GetDirectory(targetDirectory).Create();

            var sb = new StringBuilder();
            foreach (var contentFile in contentFiles)
            {
                var blocks = await ReadBlocksAsync(contentFile);
                this.AppendFile(sb, blocks, contentFile);
            }

            await this.fileSystem.File.WriteAllTextAsync(combinedPath, sb.ToString());
            written.Add(combinedPath);
        }
        else
        {
            this.fileSystem.GetDirectory(outputPath).Create();
            foreach (var contentFile in contentFiles)
            {
                var baseName = this.fileSystem.Path.GetBaseName(contentFile.Name) ?? DefaultBaseName;
                var target = this.fileSystem.Path.Combine(outputPath, baseName + MarkdownExtension);

                var blocks = await ReadBlocksAsync(contentFile);
                var sb = new StringBuilder();
                this.AppendFile(sb, blocks, contentFile);

                await this.fileSystem.File.WriteAllTextAsync(target, sb.ToString());
                written.Add(target);
            }
        }

        return written;
    }

    private static async Task<IReadOnlyList<ContentBlock>> ReadBlocksAsync(IFileInfo file)
    {
        var blocks = new List<ContentBlock>();
        await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(file))
            blocks.Add(block);
        return blocks;
    }

    private void AppendFile(StringBuilder sb, IReadOnlyList<ContentBlock> blocks, IFileInfo file)
    {
        var baseName = this.fileSystem.Path.GetBaseName(file.Name);
        var idPrefix = string.IsNullOrEmpty(baseName) ?
            "block" : // Default id prefix - blockId may be used in HTML id attributes which must start with a letter
            baseName;

        this.AppendBlocks(sb, blocks, idPrefix, baseName);
    }

    private void AppendBlocks(StringBuilder sb, IReadOnlyList<ContentBlock> blocks, string idPrefix, string? fileHeading)
    {
        if (!string.IsNullOrEmpty(fileHeading))
        {
            sb.Append($"# {fileHeading}\n\n");
        }

        ContentBlock? previousBlock = null;
        for (var i = 0; i < blocks.Count; i++)
        {
            var blockIndex = i + 1;
            var block = blocks[i];
            var blockId = $"{idPrefix}-{blockIndex}";
            var model = this.BuildBlockModel(block, blockId, previousBlock);
            this.RenderBlock(sb, model, this.blockTemplate);
            previousBlock = block;
        }
    }

    private void RenderBlock(StringBuilder sb, BlockModel model, Template template)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import("format_timecode", new Func<TimeSpan?, string?>(FormatTimecode));
        scriptObject.Import("render_audio", new Func<string?, string?>(this.RenderAudio));
        scriptObject.Import("render_image", new Func<string?, string?>(this.RenderImage));
        var context = new TemplateContext
        {
            StrictVariables = false,
            EnableRelaxedTargetAccess = true,
            NewLine = "\n",
        };
        context.PushGlobal(scriptObject);
        var output = template.Render(context);
        sb.Append(output);
    }

    private BlockModel BuildBlockModel(ContentBlock block, string blockId, ContentBlock? previousBlock)
    {
        var heading = this.settings.NoBlockHeadings ? null : GetHeading(block);

        var chunks = new List<ChunkModel>();
        if (block.Chunks.Count > 0)
        {
            // Inject formatted timecode as additional reference if there are existing references on the last chunk.
            // TODO - make this an option or a separate transform (how much 'formatting' should be done in content files?)
            // Currently only injecting timecode if there are other references (to avoid footnotes that would only have a timecode).
            var processedChunks = block.Chunks;
            var timecode = FormatTimecode(block.Source?.Timecodes?.Start);
            if (timecode is not null && block.Chunks.Any(c => c.References.Count > 0))
            {
                var last = block.Chunks[^1];
                var newRefs = last.References.SetItem("time", new Chunk { Text = timecode });
                var updated = last with { References = newRefs };
                processedChunks = block.Chunks.SetItem(block.Chunks.Count - 1, new ContentBlockChunk(updated)
                {
                    References = newRefs,
                });
            }

            for (var i = 0; i < processedChunks.Count; i++)
            {
                var chunk = processedChunks[i];
                var chunkId = processedChunks.Count > 1 ? $"{blockId}-{i + 1}" : blockId;
                chunks.Add(this.BuildChunkModel(chunk, chunkId));
            }
        }

        return new BlockModel
        {
            Settings = this.settings,
            Block = block,
            PreviousBlock = previousBlock,
            Heading = heading,
            Chunks = chunks,
        };
    }

    private static string? GetHeading(ContentBlock block) =>
        block.Source?.Note ??
        block.Source?.Resource ??
        FormatTimecode(block.Source?.Timecodes?.Start) ??
        // TODO - we should probably promote the ref source to the block when merging but there are other assumptions
        // in the downstream transforms on this right now, need to clean those up first (e.g. to use content roles instead
        // of presence/absence of Source to indicate reference/text-only blocks)
        FormatTimecode(block.References.Select(r => r.Value.Source?.Timecodes?.Start).Where(t => t.HasValue).FirstOrDefault());

    private static string? FormatTimecode(TimeSpan? start)
    {
        if (start is null)
            return null;

        // Match the PowerShell behavior of stripping fractional seconds
        return $"{(int)start.Value.TotalHours:00}:{start.Value.Minutes:00}:{start.Value.Seconds:00}";
    }

    private string? RenderAudio(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // TODO - get "audio" from constant in Media namespace - or better replace 'name' with 'path' on audio object
        var escaped = EscapeFilePathAsUri(name);
        var url = $"audio/{escaped}";

        // Use explicit <audio> tag because the anchor link that pandoc embeds within the <audio> tag
        // if ![]() is used fails EPUB3 validation and causes issues for KyBook 3 on iOS
        // TODO - file this issue against pandoc and switch to ![]() once fixed (converge with RenderImage)
        return $"""<audio src="{url}" controls="controls"></audio>""";
    }

    private string? RenderImage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // TODO - get "images" from constant in Media namespace - or better replace 'name' with 'path' on image object
        var escaped = EscapeFilePathAsUri(name);
        var url = $"images/{escaped}";
        var caption = this.fileSystem.Path.GetFileNameWithoutExtension(name);
        return $"![{caption}]({url})";
    }

    private static string EscapeFilePathAsUri(string path)
    {
        return Uri.EscapeDataString(path).Replace("%2F", "/", StringComparison.Ordinal);
    }

    private ChunkModel BuildChunkModel(ContentBlockChunk chunk, string chunkId)
    {
        var ruby = this.settings.RubyBehavior;
        var chunkText = ProcessRubyText(chunk.Text ?? string.Empty, ruby);
        if (ruby == MarkdownRubyBehavior.BlurHtml)
            chunkText = ConvertTextToBlurHtml(chunkText, chunkId, ruby: true);

        var refSb = new StringBuilder();
        var refLabels = (chunk.References.Count > 1 || this.settings.ReferenceLabels == MarkdownReferenceLabels.All) &&
            this.settings.ReferenceLabels != MarkdownReferenceLabels.None;

        foreach (var (refName, refValue) in chunk.References.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var refText = refValue.Text;
            if (string.IsNullOrEmpty(refText))
                continue;

            switch (this.settings.ReferenceBehavior)
            {
                case MarkdownReferenceBehavior.Footnotes:
                {
                    var lines = StringToLines(refText);
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (line.Length > 0)
                        {
                            if (refSb.Length > 0)
                                refSb.Append("    ");
                            if (refLabels && i == 0)
                                refSb.Append(refName).Append(": ").Append(line).Append('\n');
                            else
                                refSb.Append(line).Append('\n');
                        }
                        else
                        {
                            refSb.Append('\n');
                        }
                    }
                    refText = null;
                    break;
                }
                case MarkdownReferenceBehavior.BlurHtml:
                {
                    var refId = $"{chunkId}-ref-{refName}";
                    refText = ConvertTextToBlurHtml(refText, refId, ruby: false, label: refLabels ? refName : null);
                    break;
                }
                default:
                    if (refLabels)
                    {
                        // TODO - indentation as separate style?
                        var lines = StringToLines(refText);
                        if (lines.Count > 0)
                            lines[0] = $"{refName}: {lines[0]}";
                        foreach (var line in lines)
                            refSb.Append("> ").Append(line).Append('\n');
                        refText = null;
                    }
                    break;
            }

            if (refText is not null)
                refSb.Append(refText).Append('\n');
            refSb.Append('\n');
        }

        if (this.settings.ReferenceBehavior == MarkdownReferenceBehavior.Footnotes && refSb.Length > 0)
        {
            chunkText = $"{chunkText} [^{chunkId}]";
            refSb.Insert(0, $"[^{chunkId}]: ");

            if (this.settings.Quirks == MarkdownQuirks.KyBook3)
            {
                // Convert paragraphs to line breaks for KyBook 3 as paragraphs in footnotes are not rendered properly
                var lines = StringToLines(refSb.ToString().TrimEnd());
                refSb.Clear();
                foreach (var line in lines)
                {
                    if (line.Length > 0)
                    {
                        if (refSb.Length > 0)
                            refSb.Append('\n');
                        refSb.Append(line);
                    }
                    else
                    {
                        var last = refSb.Length > 0 ? refSb[^1] : '\0';
                        var prev = refSb.Length > 1 ? refSb[^2] : '\0';
                        var sp = last == ' '
                            ? (prev == ' ' ? string.Empty : " ")
                            : "  ";
                        refSb.Append(sp);
                    }
                }
                refSb.Append('\n');
            }
        }

        return new ChunkModel
        {
            Text = chunkText,
            RefText = refSb.Length > 0 ? refSb.ToString() : null,
        };
    }

    internal static string ProcessRubyText(string text, MarkdownRubyBehavior ruby)
    {
        if (ruby == MarkdownRubyBehavior.None)
            return text;

        var replacement = ruby switch
        {
            MarkdownRubyBehavior.Html or MarkdownRubyBehavior.BlurHtml => "<ruby>$1<rt>$2</rt></ruby>",
            MarkdownRubyBehavior.Remove => "$1",
            _ => "$0",
        };

        return RubyPatternRegex().Replace(text, replacement);
    }

    internal static string ConvertTextToBlurHtml(string text, string id, bool ruby, string? label = null)
    {
        if (ruby && !text.Contains("<ruby>", StringComparison.Ordinal))
        {
            return label is not null ? $"{label}: {text}" : text;
        }

        var html = Markdig.Markdown.ToHtml(text).TrimEnd();
        var isPara = html.StartsWith("<p>", StringComparison.Ordinal) &&
            html.EndsWith("</p>", StringComparison.Ordinal);
        var element = isPara ? (label is not null ? "span" : "p") : "div";

        var blurClass = ruby ? "blurRuby" : "blurText";
        var initial = $"<{element} class='{blurClass}'><input type='checkbox' id='{id}'/><label for='{id}'>";
        var final = $"</label></{element}>";

        html = isPara ?
            HtmlParagraphRegex().Replace(html, $"{initial}$1{final}") :
            $"{initial}{html}{final}";

        if (label is not null)
            html = $"<p><span>{label}:</span> {html}</p>";

        return html;
    }

    private static List<string> StringToLines(string s)
    {
        var lines = new List<string>();
        using var reader = new StringReader(s);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);
        return lines;
    }

    private static Template LoadDefaultBlockTemplate()
    {
        using var stream = typeof(MarkdownExporter).Assembly
            .GetManifestResourceStream(BlockTemplateResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{BlockTemplateResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return ParseBlockTemplate(reader.ReadToEnd(), BlockTemplateResourceName);
    }

    private static Template LoadBlockTemplateFromFile(IFileSystem fileSystem, string path)
    {
        var file = fileSystem.GetFile(path);
        if (!file.Exists)
            throw new FileNotFoundException($"Template file not found: {path}", path);
        var source = fileSystem.File.ReadAllText(file.FullName);
        return ParseBlockTemplate(source, path);
    }

    private static Template ParseBlockTemplate(string source, string sourceDescription)
    {
        var template = Template.Parse(source, sourceDescription);
        return template.HasErrors ?
            throw new InvalidOperationException($"Failed to parse block template '{sourceDescription}': " + string.Join("; ", template.Messages)) :
            template;
    }

    private sealed class BlockModel
    {
        public required MarkdownExportSettings Settings { get; init; }
        public required ContentBlock Block { get; init; }
        public ContentBlock? PreviousBlock { get; init; }
        public string? Heading { get; init; }
        public IReadOnlyList<ChunkModel> Chunks { get; init; } = [];
    }

    private sealed class ChunkModel
    {
        public required string Text { get; init; }
        public string? RefText { get; init; }
    }

    [GeneratedRegex(@"[ ]?(\w+)\[(\w+)\]")]
    private static partial Regex RubyPatternRegex();
    [GeneratedRegex("^<p>(.*)</p>$", RegexOptions.Singleline)]
    private static partial Regex HtmlParagraphRegex();
}
