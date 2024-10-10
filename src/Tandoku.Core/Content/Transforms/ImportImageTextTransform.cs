namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Tandoku.Images;
using Tandoku.Volume;

public sealed class ImportImageTextTransform : ContentBlockRewriter
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

        this.imagesDir = this.fileSystem.GetDirectory(this.volumeInfo.Path).GetSubdirectory("images");
    }

    public override ContentBlock? Visit(TextBlock block)
    {
        if (block.Image?.Name is var imageName &&
            imageName is not null &&
            this.TryGetImageTextBlocks(imageName, out var blocks) &&
            blocks.Count > 0)
        {
            // TODO - just add new chunks, leave existing ref on its own
            // and add merge-ref-chunks / MergeRefChunksTransform to merge with single chunk if only one, otherwise leave separate
            // later could add LLM/embedding step to identify relevant text and discard others

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
        var extension = this.provider.ImageAnalysisFileExtension;
        var imageAnalysisFile = textDir.GetFile($"{baseName}{extension}");
        if (!imageAnalysisFile.Exists)
        {
            blocks = null;
            return false;
        }

        blocks = this.provider.ReadTextBlocks(imageAnalysisFile).ToList();
        return true;
    }
}
