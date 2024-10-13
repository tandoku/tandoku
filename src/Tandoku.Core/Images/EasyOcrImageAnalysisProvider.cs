namespace Tandoku.Images;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;
using Tandoku.Content;

public sealed class EasyOcrImageAnalysisProvider : IImageAnalysisProvider
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ImageAnalysisFileExtension => ".easyocr.json";

    public async Task<IReadOnlyCollection<Chunk>> ReadTextChunksAsync(IFileInfo imageAnalysisFile)
    {
        using var stream = imageAnalysisFile.OpenRead();

        var result = await JsonSerializer.DeserializeAsync<EasyOcrResult>(stream, jsonOptions);
        if (result is null)
            return [];

        var chunks = new List<Chunk>();
        foreach (var line in result.ReadResult)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            chunks.Add(new Chunk
            {
                Image = new ChunkImage
                {
                    TextSpans = [new ImageTextSpan
                    {
                        Text = line.Text,
                        Confidence = line.Confident,
                    }],
                },
                Text = line.Text,
            });
        }
        return chunks;
    }

    private sealed record EasyOcrResult(IImmutableList<Line> ReadResult);
    private sealed record Line(string Text, double Confident);
}
