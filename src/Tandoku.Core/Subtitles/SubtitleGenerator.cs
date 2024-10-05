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
    private readonly int extendAudioMsecs;

    public SubtitleGenerator(
        SubtitlePurpose purpose,
        int extendAudioMsecs,
        IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.purpose = purpose;
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
            if (block is TextBlock textBlock)
            {
                if (this.TryCreateParagraph(
                    textBlock,
                    textBlock.Source,
                    refName: null,
                    baseName,
                    out var para))
                {
                    subtitle.Paragraphs.Add(para);
                }
                else if (textBlock.References.Count > 0)
                {
                    var reference = textBlock.References.First();
                    if (this.TryCreateParagraph(
                        reference.Value,
                        reference.Value.Source,
                        reference.Key,
                        baseName,
                        out para))
                    {
                        subtitle.Paragraphs.Add(para);
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"Only text blocks are currently supported.");
            }
        }
        subtitle.Renumber();
        return subtitle;
    }

    private bool TryCreateParagraph(
        IMarkdownText content,
        ContentSource? source,
        string? refName,
        string baseName,
        [NotNullWhen(true)] out Paragraph? paragraph)
    {
        if (!string.IsNullOrWhiteSpace(content.Text) &&
            source?.Timecodes is not null)
        {
            var text = this.purpose switch
            {
                SubtitlePurpose.MediaExtraction =>
                    $"{baseName}|{(refName is not null ? $"{refName}|" : string.Empty)}{source.Ordinal}",
                _ => content.ToPlainText(),
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
