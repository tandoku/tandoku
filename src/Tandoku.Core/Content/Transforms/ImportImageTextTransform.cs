namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.IO.Abstractions;
using Tandoku.Images;
using Tandoku.Volume;

public sealed class ImportImageTextTransform : IContentBlockTransform
{
    private readonly IImageAnalysisProvider provider;
    private readonly VolumeInfo volumeInfo;
    private readonly IFileSystem fileSystem;
    private readonly IDirectoryInfo imagesDir;

    public ImportImageTextTransform(
        IImageAnalysisProvider provider,
        VolumeInfo volumeInfo,
        IFileSystem? fileSystem = null)
    {
        this.provider = provider;
        this.volumeInfo = volumeInfo;
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
                // TODO - just add new chunks, leave existing ref on its own
                // and add merge-ref-chunks / MergeRefChunksTransform to merge with single chunk if only one, otherwise leave separate
                // later could add LLM/embedding step to identify relevant text and discard others
                if (block.Chunks.Count > 0)
                {
                    yield return block with
                    {
                        Chunks = new[] { new ContentBlockChunk(chunks.First()) with { References = block.Chunks[0].References } }.Concat(
                            block.Chunks.Skip(1)).Concat(
                            chunks.Skip(1).Select(c => new ContentBlockChunk(c))).ToImmutableList(),
                    };
                }
                else
                {
                    yield return block with
                    {
                        Chunks = block.Chunks.AddRange(chunks.Select(c => new ContentBlockChunk(c))),
                    };
                }
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
