namespace Tandoku.Images;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using Tandoku.Content;

public sealed class Acv4ImageAnalysisProvider : IImageAnalysisProvider
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ImageAnalysisFileExtension => ".acv4.json";

    public IEnumerable<TextBlock> ReadTextBlocks(IFileInfo imageAnalysisFile)
    {
        using var stream = imageAnalysisFile.OpenRead();

        var result = JsonSerializer.Deserialize<ImageAnalysisResult>(stream, jsonOptions);
        if (result is null)
            yield break;

        var lines =
            from block in result.ReadResult.Blocks
            from line in block.Lines
            select line;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            yield return new TextBlock
            {
                Image = new ContentImage
                {
                    Region = new ContentImageRegion
                    {
                        Segments = line.Words.Select(w => new ContentRegionSegment
                        {
                            Text = w.Text,
                            Confidence = w.Confidence,
                        }).ToImmutableArray(),
                    }
                },
                Text = line.Text,
            };
        }
    }

    private sealed record ImageAnalysisResult(ReadResult ReadResult);
    private sealed record ReadResult(IReadOnlyList<Block> Blocks);
    private sealed record Block(IReadOnlyList<Line> Lines);
    private sealed record Line(string Text, IReadOnlyList<Word> Words);
    private sealed record Word(string Text, double Confidence);
}
