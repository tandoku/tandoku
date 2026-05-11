namespace Tandoku.Tests.Content;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;
using Tandoku.Content.Alignment;

public class ContentMergerTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task MergeAsync_AlignsOverlappingBlocksAcrossDirectories()
    {
        this.mockFs.AddFile("/in/a.content.yaml", new MockFileData(
            "source: {timecodes: {start: '00:00:00.000', end: '00:00:02.000'}}\nchunks:\n- text: primary\n"));
        this.mockFs.AddFile("/refs/a.content.yaml", new MockFileData(
            "source: {timecodes: {start: '00:00:00.500', end: '00:00:01.500'}}\nchunks:\n- text: secondary\n"));

        var merger = new ContentMerger(this.mockFs);
        await merger.MergeAsync("/in", "/refs", "/out", new TimecodeContentAligner("subs"));

        this.Fs.File.Exists("/out/a.content.yaml").Should().BeTrue();
        var output = this.Fs.GetFile("/out/a.content.yaml").OpenText().ReadToEnd();
        output.Should().Contain("primary");
        output.Should().Contain("secondary");
        output.Should().Contain("subs:");
    }
}
