namespace Tandoku.Tests.Content;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;

public class ContentLinkerTests
{
    private readonly MockFileSystem mockFs = new();
    private readonly RamDirectoryFactory directoryFactory = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task LinkAsync_LinksMatchingPhrase_AndReportsCounts()
    {
        // Build a reference index from one content file.
        this.mockFs.AddFile("/refs/ref.content.yaml", new MockFileData(
            "id: r1\nchunks:\n- text: 学校に行きます\n"));
        await new ContentIndexBuilder(this.mockFs, this.directoryFactory)
            .BuildAsync("/refs", "/index");

        // Input has one matching block and one non-matching block.
        this.mockFs.AddFile("/input/in.content.yaml", new MockFileData(
            "id: i1\nchunks:\n- text: 学校に行きます\n---\n" +
            "id: i2\nchunks:\n- text: completely-unrelated-foo\n"));

        var linker = new ContentLinker(this.mockFs, this.directoryFactory);
        var (linked, total) = await linker.LinkAsync("/input", "/output", "/index", "src");

        linked.Should().Be(1);
        total.Should().Be(2);

        var output = this.Fs.GetFile("/output/in.content.yaml").OpenText().ReadToEnd();
        output.Should().Contain("src:");
    }
}
