namespace Tandoku.Images;

using System.IO.Abstractions;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Computes a 64-bit perceptual "average hash" (aHash) for each image and compares signatures
// by Hamming distance. Similar images produce hashes with few differing bits.
public sealed class AverageHashImageSimilarityProvider : IImageSimilarityProvider
{
    private const int HashSize = 8;

    public async Task<IImageSignature> ComputeSignatureAsync(IFileInfo imageFile)
    {
        await using var stream = imageFile.OpenRead();
        using var image = await Image.LoadAsync<L8>(stream);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(HashSize, HashSize),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Box,
        }));

        var total = 0;
        var pixels = new byte[HashSize * HashSize];
        var i = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var v = row[x].PackedValue;
                    pixels[i++] = v;
                    total += v;
                }
            }
        });

        var average = total / (double)pixels.Length;

        ulong hash = 0;
        for (var b = 0; b < pixels.Length; b++)
        {
            if (pixels[b] >= average)
                hash |= 1UL << b;
        }

        return new AverageHashImageSignature(hash);
    }
}

public readonly record struct AverageHashImageSignature(ulong Hash) : IImageSignature
{
    private const int HashBits = 64;

    public double SimilarityTo(IImageSignature other)
    {
        if (other is not AverageHashImageSignature aHash)
            throw new ArgumentException($"Expected {nameof(AverageHashImageSignature)}.", nameof(other));

        var distance = BitOperations.PopCount(this.Hash ^ aHash.Hash);
        return 1.0 - (distance / (double)HashBits);
    }
}
