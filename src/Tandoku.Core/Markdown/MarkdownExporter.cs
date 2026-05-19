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
    private const string BlockTemplateResourceName = "Tandoku.Markdown.Templates.Block.scriban-md";

    private static readonly Lazy<Template> BlockTemplate = new(LoadBlockTemplate);

    private readonly IFileSystem fileSystem;
    private readonly MarkdownExportSettings settings;

    public MarkdownExporter(MarkdownExportSettings? settings = null, IFileSystem? fileSystem = null)
    {
        this.settings = settings ?? new MarkdownExportSettings();
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<IReadOnlyList<string>> ExportAsync(string inputPath, string outputPath)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var contentFiles = inputDir.EnumerateContentFiles()
            .OrderBy(f => f.Name, this.fileSystem.Path.GetComparer())
            .ToList();

        if (contentFiles.Count == 0)
            return [];

        var written = new List<string>();

        if (this.settings.Combine)
        {
            string combinedPath;
            string targetDirectory;
            if (outputPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                combinedPath = outputPath;
                targetDirectory = this.fileSystem.Path.GetDirectoryName(outputPath) ?? string.Empty;
            }
            else
            {
                targetDirectory = outputPath;
                // TODO - consider exposing the combined output filename as a property on volume info
                // (and possibly drop the moniker, just using the cleaned title)
                combinedPath = this.fileSystem.Path.Combine(outputPath, "content.md");
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
                var baseName = this.fileSystem.Path.GetBaseName(contentFile.Name) ?? "content";
                var target = this.fileSystem.Path.Combine(outputPath, baseName + ".md");

                var blocks = await ReadBlocksAsync(contentFile);
                var sb = new StringBuilder();
                this.AppendFile(sb, blocks, contentFile);

                await this.fileSystem.File.WriteAllTextAsync(target, sb.ToString());
                written.Add(target);
            }
        }

        return written;
    }

    public string ExportToString(IReadOnlyList<ContentBlock> blocks, string idPrefix, string? fileHeading = null)
    {
        var sb = new StringBuilder();
        this.AppendBlocks(sb, blocks, idPrefix, fileHeading);
        return sb.ToString();
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
        var idPrefix = this.fileSystem.Path.GetBaseName(file.Name);
        string? fileHeading = null;
        if (string.IsNullOrEmpty(idPrefix))
        {
            // Default id prefix - blockId may be used in HTML id attributes which must start with a letter
            idPrefix = "block";
        }
        else if (this.settings.NoHeadings)
        {
            // TODO - clean up naming; consider including the file heading even when writing block headings
            fileHeading = idPrefix;
        }

        this.AppendBlocks(sb, blocks, idPrefix, fileHeading);
    }

    private void AppendBlocks(StringBuilder sb, IReadOnlyList<ContentBlock> blocks, string idPrefix, string? fileHeading)
    {
        if (this.settings.NoHeadings && !string.IsNullOrEmpty(fileHeading))
        {
            sb.Append("# ").Append(fileHeading).Append('\n').Append('\n');
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var blockIndex = i + 1;
            var block = blocks[i];
            var blockId = $"{idPrefix}-{blockIndex}";
            var model = this.BuildBlockModel(block, blockIndex, blockId);
            RenderBlock(sb, model);
        }
    }

    private static void RenderBlock(StringBuilder sb, BlockModel model)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        var context = new TemplateContext { StrictVariables = false, NewLine = "\n" };
        context.PushGlobal(scriptObject);
        var output = BlockTemplate.Value.Render(context);
        sb.Append(output);
    }

    private BlockModel BuildBlockModel(ContentBlock block, int blockIndex, string blockId)
    {
        var heading = GetHeading(block);
        var showHeading = !string.IsNullOrEmpty(heading) && !this.settings.NoHeadings;
        string? sectionHeading = null;
        if (!showHeading && blockIndex % 50 == 0)
        {
            // TODO - clean this up alongside heading styling overall
            sectionHeading = FormatTimecode(block.Source?.Timecodes?.Start);
        }

        var media = new List<string>();
        var imageMd = this.RenderMedia(block.Image?.Name, "images", heading);
        if (imageMd is not null)
            media.Add(imageMd);
        var audioMd = this.RenderMedia(block.Audio?.Name, "audio", heading);
        if (audioMd is not null)
            media.Add(audioMd);

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
            KeepTogether = this.settings.KeepTogether,
            Heading = showHeading ? heading : null,
            SectionHeading = sectionHeading,
            MediaBlocks = media,
            SuppressBlankAfterMedia = this.settings.Quirks == MarkdownQuirks.KyBook3,
            Chunks = chunks,
        };
    }

    private static string? GetHeading(ContentBlock block)
    {
        var heading = block.Source?.Note ?? block.Source?.Resource;
        if (!string.IsNullOrEmpty(heading))
            return heading;

        if (!string.IsNullOrEmpty(block.Image?.Name))
            return Path.GetFileNameWithoutExtension(block.Image.Name);

        return null;
    }

    private static string? FormatTimecode(TimeSpan? start)
    {
        if (start is null)
            return null;
        // Match the PowerShell behavior of stripping fractional seconds
        return $"{(int)start.Value.TotalHours:00}:{start.Value.Minutes:00}:{start.Value.Seconds:00}";
    }

    private string? RenderMedia(string? media, string container, string? caption)
    {
        if (string.IsNullOrEmpty(media))
            return null;

        var encoded = Uri.EscapeDataString(media).Replace("%2F", "/", StringComparison.Ordinal);
        var url = $"{container}/{encoded}";
        var suffix = this.settings.Quirks == MarkdownQuirks.KyBook3 ? "  " : string.Empty;

        if (container == "audio")
        {
            // Use explicit <audio> tag because the anchor link that pandoc embeds within the <audio> tag
            // if ![]() is used fails EPUB3 validation and causes issues for KyBook 3 on iOS
            var controls = this.settings.Quirks == MarkdownQuirks.KyBook3 ? "controls" : string.Empty;
            return $"<audio src=\"{url}\" controls=\"{controls}\"></audio>{suffix}";
        }

        return $"![{caption}]({url}){suffix}";
    }

    private ChunkModel BuildChunkModel(ContentBlockChunk chunk, string chunkId)
    {
        var ruby = this.settings.EffectiveRubyBehavior;
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

    private static Template LoadBlockTemplate()
    {
        using var stream = typeof(MarkdownExporter).Assembly
            .GetManifestResourceStream(BlockTemplateResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{BlockTemplateResourceName}' not found.");
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();
        var template = Template.Parse(source);
        return template.HasErrors ?
            throw new InvalidOperationException("Failed to parse block template: " + string.Join("; ", template.Messages)) :
            template;
    }

    private sealed class BlockModel
    {
        public bool KeepTogether { get; init; }
        public string? Heading { get; init; }
        public string? SectionHeading { get; init; }
        public IReadOnlyList<string> MediaBlocks { get; init; } = [];
        public bool SuppressBlankAfterMedia { get; init; }
        public IReadOnlyList<ChunkModel> Chunks { get; init; } = [];
    }

    private sealed class ChunkModel
    {
        public string Text { get; init; } = string.Empty;
        public string? RefText { get; init; }
    }

    [GeneratedRegex(@"[ ]?(\w+)\[(\w+)\]")]
    private static partial Regex RubyPatternRegex();
    [GeneratedRegex("^<p>(.*)</p>$", RegexOptions.Singleline)]
    private static partial Regex HtmlParagraphRegex();
}
