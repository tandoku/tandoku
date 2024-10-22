namespace Tandoku.Content.Transforms;

using System.IO.Abstractions;
using Tandoku.Images;
using Tandoku.Volume;

public sealed class ImportImageTextTransform : IContentBlockTransform
{
    private readonly IImageAnalysisProvider provider;
    private readonly VolumeInfo volumeInfo;
    private readonly ChunkRole? role;
    private readonly IFileSystem fileSystem;
    private readonly IDirectoryInfo imagesDir;

    public ImportImageTextTransform(
        IImageAnalysisProvider provider,
        VolumeInfo volumeInfo,
        ChunkRole? role = null,
        IFileSystem? fileSystem = null)
    {
        this.provider = provider;
        this.volumeInfo = volumeInfo;
        this.role = role;
        this.fileSystem = fileSystem ?? new FileSystem();

        this.imagesDir = this.fileSystem
            .GetDirectory(this.volumeInfo.Path)
            .GetSubdirectory("images");
    }

    public async IAsyncEnumerable<ContentBlock> TransformAsync(IAsyncEnumerable<ContentBlock> blocks, IFileInfo file)
    {
        await foreach (var block in blocks)
        {
            if (block.Image?.Name is var imageName &&
                imageName is not null &&
                this.GetImageAnalysisFile(imageName) is var imageAnalysisFile &&
                imageAnalysisFile?.Exists == true &&
                await this.provider.ReadTextChunksAsync(imageAnalysisFile) is var chunks &&
                chunks.Count > 0)
            {
                yield return block with
                {
                    Chunks = block.Chunks.AddRange(chunks.Select(c => new ContentBlockChunk(c) with
                    {
                        Role = this.role ?? c.Role,
                    })),
                };
            }
            else
            {
                yield return block;
            }
        }
    }

    private IFileInfo? GetImageAnalysisFile(string imageName)
    {
        var imageFile = this.imagesDir.GetFile(imageName);
        var baseName = Path.GetFileNameWithoutExtension(imageName);
        var extension = this.provider.ImageAnalysisFileExtension;
        var textDir = imageFile.Directory?.GetSubdirectory("text");
        return textDir?.GetFile($"{baseName}{extension}");
    }
}
