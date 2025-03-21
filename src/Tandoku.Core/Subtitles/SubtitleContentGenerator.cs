﻿namespace Tandoku.Subtitles;

using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
using Tandoku.Content;
using Tandoku.Serialization;

public sealed class SubtitleContentGenerator
{
    private readonly IFileSystem fileSystem;
    private readonly string inputPath;
    private readonly string outputPath;

    public SubtitleContentGenerator(string inputPath, string outputPath, IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.inputPath = inputPath;
        this.outputPath = outputPath;
    }

    public async Task GenerateContentAsync()
    {
        var inputDir = this.fileSystem.GetDirectory(this.inputPath);
        var outputDir = this.fileSystem.GetDirectory(this.outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateSubtitleFiles())
        {
            // Note - strip both subtitle extension and language code from filename (i.e. abc.ja.srt -> abc.content.yaml)
            var baseName = this.fileSystem.Path.GetFileNameWithoutExtension(inputFile.Name);
            var targetName = this.fileSystem.Path.ChangeExtension(baseName, ".content.yaml");
            var outputFile = outputDir.GetFile(targetName);
            await YamlSerializer.WriteStreamAsync(outputFile, GenerateContentBlocksAsync(inputFile));
        }
    }

    private static async IAsyncEnumerable<ContentBlock> GenerateContentBlocksAsync(IFileInfo inputFile)
    {
        var subtitle = Subtitle.Parse(inputFile.FullName);
        var ordinal = 0;
        foreach (var para in subtitle.Paragraphs)
        {
            ordinal++;

            // TODO #7 - move these into proper transforms
            if (para.StartTime.TimeSpan > para.EndTime.TimeSpan)
                continue;
            var text = para.Text;
            text = Regex.Replace(para.Text, @"\{.+?\}", string.Empty);
            if (string.IsNullOrWhiteSpace(text) || text.Length == 1)
                continue;

            yield return new ContentBlock
            {
                Source = new BlockSource
                {
                    Ordinal = ordinal,
                    Timecodes = new TimecodePair(para.StartTime.TimeSpan, para.EndTime.TimeSpan),
                },
                Chunks = [new ContentBlockChunk
                {
                    Text = ConvertSubtitleText(text)
                }],
            };
        }
    }

    // TODO - return each line as separate chunk for additional processing
    private static string ConvertSubtitleText(string text)
    {
        var lineReader = new StringReader(text);
        var textBuilder = new StringBuilder();
        bool first = true;
        while (true)
        {
            var line = lineReader.ReadLine();
            if (line is not null)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    textBuilder.Append("  ");
                    textBuilder.AppendLine();
                }
                textBuilder.Append(line);
            }
            else
            {
                break;
            }
        }
        return textBuilder.ToString();
    }
}
