namespace Tandoku.Content.Transforms;

using System.IO.Abstractions;
using Tandoku.Images;
using Tandoku.Volume;

public static class GroupSimilarImagesTransform
{
    public static GroupSimilarImagesTransform<TSignature> Create<TSignature>(
        IImageSimilarityProvider<TSignature> provider,
        double similarityThreshold,
        VolumeInfo volumeInfo,
        IFileSystem? fileSystem = null)
        where TSignature : IImageSignature<TSignature> =>
        new(provider, similarityThreshold, volumeInfo, fileSystem);
}

public sealed class GroupSimilarImagesTransform<TSignature> : IContentBlockTransform
    where TSignature : IImageSignature<TSignature>
{
    private readonly IImageSimilarityProvider<TSignature> provider;
    private readonly double similarityThreshold;
    private readonly IFileSystem fileSystem;
    private readonly IDirectoryInfo imagesDir;

    public GroupSimilarImagesTransform(
        IImageSimilarityProvider<TSignature> provider,
        double similarityThreshold,
        VolumeInfo volumeInfo,
        IFileSystem? fileSystem = null)
    {
        this.provider = provider;
        this.similarityThreshold = similarityThreshold;
        this.fileSystem = fileSystem ?? new FileSystem();

        this.imagesDir = this.fileSystem
            .GetDirectory(volumeInfo.Path)
            .GetSubdirectory("images");
    }

    public async IAsyncEnumerable<ContentBlock> TransformAsync(IAsyncEnumerable<ContentBlock> blocks, IFileInfo file)
    {
        string? groupLeaderName = null;
        // ! not used until initialized (due to groupLeaderName is not null check) but compiler cannot verify this
        TSignature groupLeaderSignature = default!;

        await foreach (var block in blocks)
        {
            if (block.Image?.Name is not string imageName)
            {
                yield return block;
                continue;
            }

            var imageFile = this.imagesDir.GetFile(imageName);
            if (!imageFile.Exists)
            {
                yield return block;
                continue;
            }

            var signature = await this.provider.ComputeSignatureAsync(imageFile);

            if (groupLeaderName is not null)
            {
                var similarity = signature.SimilarityTo(groupLeaderSignature);
                var groupInfo = new BlockImageGroup
                {
                    Name = groupLeaderName,
                    Similarity = similarity,
                };

                if (similarity >= this.similarityThreshold)
                {
                    yield return block with
                    {
                        Image = block.Image with { Group = groupInfo },
                    };
                    continue;
                }

                yield return block with
                {
                    Image = block.Image with { GroupCandidate = groupInfo },
                };
            }
            else
            {
                yield return block;
            }

            groupLeaderName = imageName;
            groupLeaderSignature = signature;
        }
    }
}
