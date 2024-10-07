namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text.Json;
using Tandoku.Volume;

// TODO - make this an interface instead
public enum ImageAnalysisProvider
{
    Acv4,
    // TODO - EasyOcr
}

public sealed class ImportImageTextTransform : ContentBlockRewriter
{
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ImageAnalysisProvider provider;
    private readonly VolumeInfo volumeInfo;
    private readonly IFileSystem fileSystem;
    private readonly IDirectoryInfo imagesDir;

    public ImportImageTextTransform(
        ImageAnalysisProvider provider,
        VolumeInfo volumeInfo,
        IFileSystem? fileSystem = null)
    {
        this.provider = provider;
        this.volumeInfo = volumeInfo;
        this.fileSystem = fileSystem ?? new FileSystem();

        this.imagesDir = this.fileSystem.GetDirectory(this.volumeInfo.Path).GetSubdirectory("images");
    }

    public override ContentBlock? Visit(TextBlock block)
    {
        if (block.Image?.Name is var imageName &&
            imageName is not null &&
            this.TryGetImageTextBlocks(imageName, out var blocks) &&
            blocks.Count > 0)
        {
            if (blocks.Count == 1)
            {
                return block with
                {
                    Image = block.Image! with
                    {
                        Region = blocks[0].Image?.Region,
                    },
                    Text = blocks[0].Text,
                };
            }
            else
            {
                blocks[0] = blocks[0] with { References = block.References };

                // TODO - TextBlock.ConvertToComposite()
                return new CompositeBlock
                {
                    Id = block.Id,
                    Image = block.Image,
                    Audio = block.Audio,
                    Blocks = blocks.ToImmutableArray(),
                    Source = block.Source,
                };
            }
        }
        return block;
    }

    private bool TryGetImageTextBlocks(
        string imageName,
        [NotNullWhen(true)] out List<TextBlock>? blocks)
    {
        var imageFile = this.imagesDir.GetFile(imageName);
        if (imageFile.Directory?.GetSubdirectory("text") is var textDir && textDir is null)
        {
            blocks = null;
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(imageName);
        var providerName = this.provider.ToString().ToLowerInvariant();
        var imageAnalysisFile = textDir.GetFile($"{baseName}.{providerName}.json");
        if (!imageAnalysisFile.Exists)
        {
            blocks = null;
            return false;
        }

        blocks = (this.provider switch
        {
            ImageAnalysisProvider.Acv4 => ReadAcv4TextBlocks(imageAnalysisFile),
            _ => throw new ArgumentException($"Unknown provider '{this.provider}"),
        }).ToList();
        return true;
    }

    private static IEnumerable<TextBlock> ReadAcv4TextBlocks(IFileInfo imageAnalysisFile)
    {
        using var stream = imageAnalysisFile.OpenRead();
        var result = JsonSerializer.Deserialize<ImageAnalysisResult>(stream, jsonOptions);
        var lines =
            from block in result?.ReadResult.Blocks
            from line in block.Lines
            select line;
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line.Text))
            {
                yield return new TextBlock
                {
                    Image = new ContentImage
                    {
                        Region = new ContentImageRegion
                        {
                            Segments = line.Words.Select(w => new ContentRegionSegment
                            {
                                Text = w.Text,
                                Confidence = w.Confidence
                            }).ToImmutableArray(),
                        }
                    },
                    Text = line.Text,
                };
            }
        }
    }

    private sealed record ImageAnalysisResult(ReadResult ReadResult);
    private sealed record ReadResult(IReadOnlyList<Block> Blocks);
    private sealed record Block(IReadOnlyList<Line> Lines);
    private sealed record Line(string Text, IReadOnlyList<Word> Words);
    private sealed record Word(string Text, double Confidence);
}
