namespace Tandoku.Images;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;
using Tandoku.Content;

public sealed class Acv4ImageAnalysisProvider : IImageAnalysisProvider
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ImageAnalysisFileExtension => ".acv4.json";

    public async Task<IReadOnlyCollection<Chunk>> ReadTextChunksAsync(IFileInfo imageAnalysisFile)
    {
        using var stream = imageAnalysisFile.OpenRead();

        var result = await JsonSerializer.DeserializeAsync<ImageAnalysisResult>(stream, jsonOptions);
        if (result is null)
            return [];

        var lines =
            from block in result.ReadResult.Blocks
            from line in block.Lines
            select line;

        var chunks = new List<Chunk>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            chunks.Add(new Chunk
            {
                Image = new ChunkImage
                {
                    TextSpans = line.Words.Select(w => new ImageTextSpan
                    {
                        Text = w.Text,
                        Confidence = w.Confidence,
                    }).ToImmutableList(),
                },
                Text = line.Text,
            });
        }
        return chunks;
    }

    private sealed record ImageAnalysisResult(ReadResult ReadResult);
    private sealed record ReadResult(IReadOnlyList<Block> Blocks);
    private sealed record Block(IReadOnlyList<Line> Lines);
    private sealed record Line(string Text, IReadOnlyList<Word> Words);
    private sealed record Word(string Text, double Confidence);
}
