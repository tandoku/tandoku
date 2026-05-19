namespace Tandoku.Tests.Content.Transforms;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;
using Tandoku.Content.Transforms;
using Tandoku.Images;
using Tandoku.Volume;

public class GroupSimilarImagesTransformTests
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
    public async Task FirstBlockWithImage_NoAnnotation()
    {
        this.mockFs.AddFile("/vol/images/a.png", new MockFileData("a"));
        var provider = new FakeProvider { ["a.png"] = 0 };
        var transform = new GroupSimilarImagesTransform(provider, 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(BlockWithImage("a.png")),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Single().Image!.Group.Should().BeNull();
        result.Single().Image!.GroupCandidate.Should().BeNull();
    }

    [Test]
    public async Task SimilarImage_AnnotatedWithGroup_PointingToLeader()
    {
        this.mockFs.AddFile("/vol/images/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/vol/images/b.png", new MockFileData("b"));
        var provider = new FakeProvider
        {
            ["a.png"] = 0UL,
            ["b.png"] = 0b1UL, // 1 bit different out of 64 -> similarity = 63/64
        };
        var transform = new GroupSimilarImagesTransform(provider, 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(BlockWithImage("a.png"), BlockWithImage("b.png")),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result[0].Image!.Group.Should().BeNull();
        result[1].Image!.Group.Should().NotBeNull();
        result[1].Image!.Group!.Name.Should().Be("a.png");
        result[1].Image!.Group!.Similarity.Should().BeApproximately(63.0 / 64.0, 1e-9);
        result[1].Image!.GroupCandidate.Should().BeNull();
    }

    [Test]
    public async Task DissimilarImage_AnnotatedWithGroupCandidate_AndBecomesNewLeader()
    {
        this.mockFs.AddFile("/vol/images/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/vol/images/b.png", new MockFileData("b"));
        this.mockFs.AddFile("/vol/images/c.png", new MockFileData("c"));
        var provider = new FakeProvider
        {
            ["a.png"] = 0UL,
            ["b.png"] = 0xFFFFFFFFUL, // 32 bits different -> similarity 0.5
            ["c.png"] = 0xFFFFFFFFUL,
        };
        var transform = new GroupSimilarImagesTransform(provider, 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(BlockWithImage("a.png"), BlockWithImage("b.png"), BlockWithImage("c.png")),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        // b vs a -> below threshold, GroupCandidate, b becomes new leader
        result[1].Image!.GroupCandidate.Should().NotBeNull();
        result[1].Image!.GroupCandidate!.Name.Should().Be("a.png");
        result[1].Image!.GroupCandidate!.Similarity.Should().BeApproximately(0.5, 1e-9);
        result[1].Image!.Group.Should().BeNull();

        // c vs b -> identical, Group pointing to b
        result[2].Image!.Group.Should().NotBeNull();
        result[2].Image!.Group!.Name.Should().Be("b.png");
        result[2].Image!.Group!.Similarity.Should().BeApproximately(1.0, 1e-9);
    }

    [Test]
    public async Task GroupedBlock_NextBlockComparedToLeader_NotPreviousBlock()
    {
        // a, b (similar to a -> group=a), c (similar to a -> group=a; should be compared to a, not b)
        this.mockFs.AddFile("/vol/images/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/vol/images/b.png", new MockFileData("b"));
        this.mockFs.AddFile("/vol/images/c.png", new MockFileData("c"));
        var provider = new FakeProvider
        {
            ["a.png"] = 0UL,
            ["b.png"] = 0b11UL, // 2 bits off from a
            ["c.png"] = 0b101UL, // 2 bits off from a, 2 bits off from b
        };
        var transform = new GroupSimilarImagesTransform(provider, 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(BlockWithImage("a.png"), BlockWithImage("b.png"), BlockWithImage("c.png")),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result[1].Image!.Group!.Name.Should().Be("a.png");
        result[2].Image!.Group!.Name.Should().Be("a.png");
        result[2].Image!.Group!.Similarity.Should().BeApproximately(62.0 / 64.0, 1e-9);
    }

    [Test]
    public async Task BlockWithoutImage_PassedThroughUnchanged()
    {
        var transform = new GroupSimilarImagesTransform(new FakeProvider(), 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(new ContentBlock { Chunks = [new ContentBlockChunk { Text = "x" }] }),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Single().Image.Should().BeNull();
    }

    [Test]
    public async Task MissingImageFile_BlockPassedThrough()
    {
        var transform = new GroupSimilarImagesTransform(new FakeProvider(), 0.9, this.VolumeAt("/vol"), this.mockFs);

        var result = await transform.TransformAsync(
            AsAsync(BlockWithImage("missing.png")),
            this.Fs.GetFile("/vol/c.content.yaml")).ToList();

        result.Single().Image!.Group.Should().BeNull();
        result.Single().Image!.GroupCandidate.Should().BeNull();
    }

    private static ContentBlock BlockWithImage(string name) =>
        new() { Image = new BlockImage { Name = name } };

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private sealed class FakeProvider : Dictionary<string, ulong>, IImageSimilarityProvider
    {
        public Task<IImageSignature> ComputeSignatureAsync(IFileInfo imageFile) =>
            Task.FromResult<IImageSignature>(new AverageHashImageSignature(this[imageFile.Name]));
    }
}
