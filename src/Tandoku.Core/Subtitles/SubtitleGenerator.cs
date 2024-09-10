namespace Tandoku.Subtitles;

using System.IO.Abstractions;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Tandoku.Content;
using Tandoku.Serialization;

public sealed class SubtitleGenerator
{
    private readonly IFileSystem fileSystem;

    public SubtitleGenerator(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task GenerateAsync(string inputPath, string outputPath)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var outputDir = this.fileSystem.GetDirectory(outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateContentFiles())
        {
            var baseName = inputFile.GetBaseName();
            var targetName = $"{baseName}.srt";
            var outputFile = outputDir.GetFile(targetName);

            var subtitle = await GenerateSubtitleAsync(
                YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile));

            await this.fileSystem.File.WriteAllTextAsync(
                outputFile.FullName,
                subtitle.ToText(new SubRip()));
        }
    }

    private static async Task<Subtitle> GenerateSubtitleAsync(IAsyncEnumerable<ContentBlock> blocks)
    {
        var subtitle = new Subtitle();
        await foreach(var block in blocks)
        {
            if (block is TextBlock textBlock)
            {
                if (!string.IsNullOrWhiteSpace(textBlock.Text) &&
                    block.Source?.Timecodes is not null)
                {
                    var para = new Paragraph(
                        textBlock.ToPlainText(),
                        block.Source.Timecodes.Value.Start.TotalMilliseconds,
                        block.Source.Timecodes.Value.End.TotalMilliseconds);

                    subtitle.Paragraphs.Add(para);
                }
                else if (textBlock.References.Count > 0)
                {
                    var reference = textBlock.References.First().Value;
                    if (!string.IsNullOrEmpty(reference.Text) &&
                        reference.Source?.Timecodes is not null)
                    {
                        var para = new Paragraph(
                            reference.ToPlainText(),
                            reference.Source.Timecodes.Value.Start.TotalMilliseconds,
                            reference.Source.Timecodes.Value.End.TotalMilliseconds);

                        subtitle.Paragraphs.Add(para);
                    }
                }
            }
        }
        return subtitle;
    }
}
