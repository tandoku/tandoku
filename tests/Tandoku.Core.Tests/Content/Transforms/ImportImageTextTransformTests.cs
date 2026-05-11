namespace Tandoku.Tests.Content.Transforms;

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;
using Tandoku.Content.Transforms;
using Tandoku.Images;
using Tandoku.Volume;

public class ImportImageTextTransformTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    private VolumeInfo VolumeAt(string path) => new(
        Path: path,
        Slug: "vol",
        Version: VolumeVersion.Latest,
        DefinitionPath: $"{path}/volume.yaml",
        Definition: new VolumeDefinition { Language = "ja" });

    [Test]
    public async Task BlockWithoutImage_PassedThrough()
    {
        var transform = new ImportImageTextTransform(
            new FakeProvider(".json"),
            this.VolumeAt("/vol"),
            fileSystem: this.mockFs);

        var blocks = AsAsync(new ContentBlock { Chunks = [new ContentBlockChunk { Text = "x" }] });
        var result = await transform.TransformAsync(blocks, this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Should().HaveCount(1);
        result[0].Chunks.Should().HaveCount(1);
    }

    [Test]
    public async Task BlockWithImage_NoAnalysisFile_PassedThrough()
    {
        var transform = new ImportImageTextTransform(
            new FakeProvider(".json"),
            this.VolumeAt("/vol"),
            fileSystem: this.mockFs);

        var blocks = AsAsync(new ContentBlock
        {
            Image = new BlockImage { Name = "missing.png" },
            Chunks = [new ContentBlockChunk { Text = "orig" }],
        });
        var result = await transform.TransformAsync(blocks, this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Should().HaveCount(1);
        result[0].Chunks.Single().Text.Should().Be("orig");
    }

    [Test]
    public async Task BlockWithImage_AnalysisFilePresent_AppendsChunks_WithAssignedRole()
    {
        // Provider expects analysis file at /vol/images/text/<basename>.json
        this.mockFs.AddFile("/vol/images/text/scene1.json", new MockFileData("ignored-by-fake"));

        var provider = new FakeProvider(".json")
        {
            Chunks = ImmutableList.Create<Chunk>(
                new Chunk { Text = "imported text" }),
        };
        var transform = new ImportImageTextTransform(
            provider,
            this.VolumeAt("/vol"),
            role: ChunkRole.OnScreenText,
            fileSystem: this.mockFs);

        var blocks = AsAsync(new ContentBlock
        {
            Image = new BlockImage { Name = "scene1.png" },
            Chunks = [new ContentBlockChunk { Text = "orig" }],
        });

        var result = await transform.TransformAsync(blocks, this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Should().HaveCount(1);
        result[0].Chunks.Should().HaveCount(2);
        result[0].Chunks[1].Text.Should().Be("imported text");
        result[0].Chunks[1].Role.Should().Be(ChunkRole.OnScreenText);
    }

    [Test]
    public async Task ProviderReturnsNoChunks_BlockUnchanged()
    {
        this.mockFs.AddFile("/vol/images/text/scene1.json", new MockFileData("ignored"));
        var provider = new FakeProvider(".json") { Chunks = ImmutableList<Chunk>.Empty };
        var transform = new ImportImageTextTransform(provider, this.VolumeAt("/vol"), fileSystem: this.mockFs);

        var blocks = AsAsync(new ContentBlock
        {
            Image = new BlockImage { Name = "scene1.png" },
            Chunks = [new ContentBlockChunk { Text = "orig" }],
        });

        var result = await transform.TransformAsync(blocks, this.Fs.GetFile("/vol/c.content.yaml")).ToList();
        result.Single().Chunks.Should().HaveCount(1);
    }

    private sealed class FakeProvider(string extension) : IImageAnalysisProvider
    {
        public string ImageAnalysisFileExtension { get; } = extension;
        public IReadOnlyCollection<Chunk> Chunks { get; init; } = [];

        public Task<IReadOnlyCollection<Chunk>> ReadTextChunksAsync(IFileInfo imageAnalysisFile) =>
            Task.FromResult(this.Chunks);
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
