namespace Tandoku.Tests.Content;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;

public class ContentIndexBuilderTests
{
    private readonly MockFileSystem mockFs = new();
    private readonly RamDirectoryFactory directoryFactory = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task BuildAndSearch_FindsExactPhrase()
    {
        this.mockFs.AddFile("/content/a.content.yaml", new MockFileData(
            "id: a1\nchunks:\n- text: 私は学生です\n"));
        this.mockFs.AddFile("/content/b.content.yaml", new MockFileData(
            "id: b1\nchunks:\n- text: これは本です\n"));

        await new ContentIndexBuilder(this.mockFs, this.directoryFactory)
            .BuildAsync("/content", "/index");

        var searcher = new ContentIndexSearcher(this.mockFs, this.directoryFactory);
        var hits = new List<ContentBlock>();
        await foreach (var b in searcher.FindBlocksAsync("学生", "/index"))
            hits.Add(b);

        hits.Should().NotBeEmpty();
        hits[0].Id.Should().Be("a1");
    }

    [Test]
    public async Task SearchWithoutHit_ReturnsEmpty()
    {
        this.mockFs.AddFile("/content/a.content.yaml", new MockFileData(
            "chunks:\n- text: hello world\n"));

        await new ContentIndexBuilder(this.mockFs, this.directoryFactory)
            .BuildAsync("/content", "/index");

        var searcher = new ContentIndexSearcher(this.mockFs, this.directoryFactory);
        var hits = new List<ContentBlock>();
        await foreach (var b in searcher.FindBlocksAsync("nonexistent-phrase-xyz", "/index"))
            hits.Add(b);

        hits.Should().BeEmpty();
    }

    [Test]
    public async Task BlocksWithoutText_NotIndexed()
    {
        this.mockFs.AddFile("/content/a.content.yaml", new MockFileData(
            "id: empty\nchunks:\n- {}\n"));

        await new ContentIndexBuilder(this.mockFs, this.directoryFactory)
            .BuildAsync("/content", "/index");

        var searcher = new ContentIndexSearcher(this.mockFs, this.directoryFactory);
        var hits = new List<ContentBlock>();
        await foreach (var b in searcher.FindBlocksAsync("anything", "/index"))
            hits.Add(b);
        hits.Should().BeEmpty();
    }
}
