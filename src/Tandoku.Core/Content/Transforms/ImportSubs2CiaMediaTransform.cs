namespace Tandoku.Content.Transforms;

using System.IO.Abstractions;

public sealed class ImportSubs2CiaMediaTransform : ContentBlockRewriter
{
    private const string ImageExtension = ".jpg";
    private const string AudioExtension = ".mp3";

    private readonly IFileSystem fileSystem;
    private readonly MediaCollection mediaCollection;
    private readonly IDirectoryInfo mediaDir;
    private readonly string? imagePrefix;
    private readonly string? audioPrefix;

    public ImportSubs2CiaMediaTransform(
        string mediaPath,
        string? imagePrefix,
        string? audioPrefix,
        MediaCollection mediaCollection,
        IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.mediaDir = this.fileSystem.GetDirectory(mediaPath);
        this.imagePrefix = imagePrefix;
        this.audioPrefix = audioPrefix;
        this.mediaCollection = mediaCollection;
    }

    public override ContentBlock? Visit(TextBlock block)
    {
        if (block.Source?.Timecodes is not null)
        {
            block = this.ImportMedia(block, block.Source.Timecodes.Value);
        }
        else if (block.References.FirstOrDefault().Value is var reference &&
            reference?.Source?.Timecodes is not null)
        {
            block = this.ImportMedia(block, reference.Source.Timecodes.Value);
        }
        return block;
    }

    private TextBlock ImportMedia(TextBlock block, TimecodePair timecodes)
    {
        var timecodeSuffix = $"_{timecodes.Start.TotalMilliseconds}-{timecodes.End.TotalMilliseconds}";
        var baseName = this.CurrentFile?.GetBaseName();

        var imageName = $"{baseName}{timecodeSuffix}{ImageExtension}";
        var imageFile = this.mediaDir.GetFile(imageName);
        if (imageFile.Exists)
        {
            block = block with
            {
                Image = new ContentImage { Name = $"{this.imagePrefix}{imageName}" },
            };
            this.mediaCollection.Images.Add(imageFile.FullName);
        }

        var audioName = $"{baseName}{timecodeSuffix}{AudioExtension}";
        var audioFile = this.mediaDir.GetFile(audioName);
        if (audioFile.Exists)
        {
            block = block with
            {
                Audio = new ContentAudio { Name = $"{this.audioPrefix}{audioName}" },
            };
            this.mediaCollection.Audio.Add(audioFile.FullName);
        }

        return block;
    }
}

public sealed class MediaCollection
{
    public List<string> Images { get; } = [];
    public List<string> Audio { get; } = [];
}
