namespace Tandoku.Subtitles;

using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Tandoku.Content;
using Tandoku.Serialization;

public enum SubtitlePurpose
{
    Default,
    MediaExtraction,
}

public sealed class SubtitleGenerator
{
    private readonly IFileSystem fileSystem;
    private readonly SubtitlePurpose purpose;
    private readonly string? includeReference;
    private readonly int extendAudioMsecs;

    public SubtitleGenerator(
        SubtitlePurpose purpose,
        string? includeReference,
        int extendAudioMsecs,
        IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.purpose = purpose;
        this.includeReference = includeReference;
        this.extendAudioMsecs = extendAudioMsecs;
    }

    public async Task GenerateAsync(string inputPath, string outputPath)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var outputDir = this.fileSystem.GetDirectory(outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateContentFiles())
        {
            var baseName = inputFile.GetBaseName();
            if (string.IsNullOrWhiteSpace(baseName))
                throw new InvalidDataException($"Cannot derive base name from input file {inputFile}");

            var targetName = $"{baseName}.srt";
            var outputFile = outputDir.GetFile(targetName);

            var subtitle = await this.GenerateSubtitleAsync(
                YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile),
                baseName);

            await this.fileSystem.File.WriteAllTextAsync(
                outputFile.FullName,
                subtitle.ToText(new SubRip()));
        }
    }

    private async Task<Subtitle> GenerateSubtitleAsync(
        IAsyncEnumerable<ContentBlock> blocks,
        string baseName)
    {
        var subtitle = new Subtitle();
        await foreach(var block in blocks)
        {
            if (this.TryCreateParagraph(
                block.Chunks,
                block.Source,
                refName: null,
                baseName,
                out var para))
            {
                subtitle.Paragraphs.Add(para);
            }
            else if (this.includeReference is not null &&
                block.References.TryGetValue(this.includeReference, out var reference) &&
                this.TryCreateParagraph(
                    block.Chunks.Select(c =>
                        c.References.TryGetValue(this.includeReference, out var refChunk) ? refChunk : null),
                    reference.Source,
                    this.includeReference,
                    baseName,
                    out para))
            {
                subtitle.Paragraphs.Add(para);
            }
        }
        subtitle.Renumber();
        return subtitle;
    }

    private bool TryCreateParagraph(
        IEnumerable<Chunk?> chunks,
        BlockSource? source,
        string? refName,
        string baseName,
        [NotNullWhen(true)] out Paragraph? paragraph)
    {
        if (chunks.Any(c => !string.IsNullOrWhiteSpace(c?.Text)) &&
            source?.Timecodes is not null)
        {
            var text = this.purpose switch
            {
                SubtitlePurpose.MediaExtraction =>
                    $"{baseName}|{(refName is not null ? $"{refName}|" : string.Empty)}{source.Ordinal}",
                _ => chunks.CombineText(MarkdownSeparator.LineBreak).ToPlainText(),
            };

            var start = source.Timecodes.Value.Start.TotalMilliseconds;
            var end = source.Timecodes.Value.End.TotalMilliseconds;
            if (refName is not null)
            {
                // Adjust ref-only subtitles to start halfway through the original time
                // so on-screen text screenshot is captured correctly.
                // Note - alternately we could always capture screenshot from halfway time
                // of a subtitle rather than at the start but subs2cia can't do this currently.
                // Also adjust duration to 100ms at most since we won't keep audio anyway.
                start += (end - start) / 2;
                end = Math.Min(start + 100, end);
            }
            else
            {
                end += this.extendAudioMsecs;
            }

            paragraph = new Paragraph(text, start, end);
            return true;
        }
        paragraph = null;
        return false;
    }
}
