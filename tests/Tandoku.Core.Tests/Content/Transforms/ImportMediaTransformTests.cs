namespace Tandoku.Tests.Content.Transforms;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;
using Tandoku.Content.Transforms;

public class ImportMediaTransformTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task ImportsImageAndAudio_FromMatchingOrdinalRow()
    {
        // tsv row format: <text>\t?\t<audio>\t<image>\t?\t?
        var tsv =
            "ep01|3\tx\t[sound:clip3.mp3]\t<img src='shot3.png'>\tx\tx\n" +
            "ep01|7\tx\t[sound:clip7.mp3]\t<img src='shot7.png'>\tx\tx\n";
        this.mockFs.AddFile("/media/ep01/ep01.tsv", new MockFileData(tsv));
        this.mockFs.AddFile("/media/ep01/clip3.mp3", new MockFileData(""));
        this.mockFs.AddFile("/media/ep01/shot3.png", new MockFileData(""));

        var collection = new MediaCollection();
        var transform = new ImportMediaTransform(
            mediaPath: "/media",
            imagePrefix: "img-",
            audioPrefix: "aud-",
            mediaCollection: collection,
            fileSystem: this.mockFs);

        var blocks = AsAsync(new ContentBlock { Source = new BlockSource { Ordinal = 3 } });
        var contentFile = this.Fs.GetFile("/work/ep01.content.yaml");
        // The transform derives base name from contentFile via GetBaseName(). For .content.yaml
        // we need the file to actually look like a content yaml file, which works with a virtual path.
        this.mockFs.AddFile(contentFile.FullName, new MockFileData(""));

        var result = await transform.TransformAsync(blocks, this.Fs.GetFile(contentFile.FullName)).ToList();

        result.Should().HaveCount(1);
        result[0].Image!.Name.Should().Be("img-shot3.png");
        result[0].Audio!.Name.Should().Be("aud-clip3.mp3");
        collection.Images.Should().Contain(p => p.EndsWith("shot3.png"));
        collection.Audio.Should().Contain(p => p.EndsWith("clip3.mp3"));
    }

    [Test]
    public async Task NoMatchingOrdinal_ReturnsBlockUnchanged()
    {
        this.mockFs.AddFile("/media/ep01/ep01.tsv", new MockFileData(
            "ep01|1\tx\t[sound:c.mp3]\t<img src='i.png'>\tx\tx\n"));
        this.mockFs.AddFile("/work/ep01.content.yaml", new MockFileData(""));

        var transform = new ImportMediaTransform("/media", null, null, new MediaCollection(), this.mockFs);
        var blocks = AsAsync(new ContentBlock { Source = new BlockSource { Ordinal = 99 } });
        var result = await transform.TransformAsync(blocks, this.Fs.GetFile("/work/ep01.content.yaml")).ToList();

        result[0].Image.Should().BeNull();
        result[0].Audio.Should().BeNull();
    }

    [Test]
    public async Task BadAudioFormat_ThrowsInvalidData()
    {
        this.mockFs.AddFile("/media/ep01/ep01.tsv", new MockFileData(
            "ep01|1\tx\tnot-a-sound-tag\t<img src='i.png'>\tx\tx\n"));
        this.mockFs.AddFile("/work/ep01.content.yaml", new MockFileData(""));

        var transform = new ImportMediaTransform("/media", null, null, new MediaCollection(), this.mockFs);
        var blocks = AsAsync(new ContentBlock { Source = new BlockSource { Ordinal = 1 } });

        Func<Task> act = async () =>
        {
            await foreach (var _ in transform.TransformAsync(blocks, this.Fs.GetFile("/work/ep01.content.yaml"))) { }
        };
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Test]
    public async Task MissingMetadataFile_Throws()
    {
        this.mockFs.AddFile("/work/ep01.content.yaml", new MockFileData(""));
        var transform = new ImportMediaTransform("/media", null, null, new MediaCollection(), this.mockFs);
        var blocks = AsAsync(new ContentBlock { Source = new BlockSource { Ordinal = 1 } });

        Func<Task> act = async () =>
        {
            await foreach (var _ in transform.TransformAsync(blocks, this.Fs.GetFile("/work/ep01.content.yaml"))) { }
        };
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Missing media metadata file*");
    }

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
