namespace Tandoku.Media;

using System.Buffers.Binary;
using System.IO.Abstractions;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Computes a 64-bit perceptual "average hash" (aHash) for each image and compares signatures
// by Hamming distance. Similar images produce hashes with few differing bits.
public sealed class AverageHashImageSimilarityProvider : IImageSimilarityProvider<AverageHashImageSignature>
{
    private const int HashSize = 8;

    public async Task<AverageHashImageSignature> ComputeSignatureAsync(IFileInfo imageFile)
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

public readonly record struct AverageHashImageSignature(ulong Hash) : ISerializableImageSignature<AverageHashImageSignature>
{
    private const int HashBits = 64;
    private const int HashByteCount = sizeof(ulong);

    public double SimilarityTo(AverageHashImageSignature other)
    {
        var distance = BitOperations.PopCount(this.Hash ^ other.Hash);
        return 1.0 - (distance / (double)HashBits);
    }

    public static async Task<AverageHashImageSignature> ReadAsync(IFileInfo cacheFile)
    {
        var bytes = await cacheFile.FileSystem.File.ReadAllBytesAsync(cacheFile.FullName);
        if (bytes.Length != HashByteCount)
        {
            throw new InvalidDataException(
                $"Expected {HashByteCount} bytes in '{cacheFile.FullName}' but found {bytes.Length}.");
        }
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return new AverageHashImageSignature(hash);
    }

    public Task WriteAsync(IFileInfo cacheFile)
    {
        var bytes = new byte[HashByteCount];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, this.Hash);
        return cacheFile.FileSystem.File.WriteAllBytesAsync(cacheFile.FullName, bytes);
    }
}
