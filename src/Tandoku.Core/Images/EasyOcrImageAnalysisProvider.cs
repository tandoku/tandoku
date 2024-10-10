namespace Tandoku.Images;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using Tandoku.Content;

public sealed class EasyOcrImageAnalysisProvider : IImageAnalysisProvider
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ImageAnalysisFileExtension => ".easyocr.json";

    public IEnumerable<TextBlock> ReadTextBlocks(IFileInfo imageAnalysisFile)
    {
        using var stream = imageAnalysisFile.OpenRead();

        var result = JsonSerializer.Deserialize<EasyOcrResult>(stream, jsonOptions);
        if (result is null)
            yield break;

        foreach (var line in result.ReadResult)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            yield return new TextBlock
            {
                Image = new ContentImage
                {
                    Region = new ContentImageRegion
                    {
                        Segments = [new ContentRegionSegment
                        {
                            Text = line.Text,
                            Confidence = line.Confident,
                        }],
                    }
                },
                Text = line.Text,
            };
        }
    }

    private sealed record EasyOcrResult(IImmutableList<Line> ReadResult);
    private sealed record Line(string Text, double Confident);
}
