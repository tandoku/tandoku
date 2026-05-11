namespace Tandoku.Tests.Content;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;

public class ContentTransformerTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task TransformAsync_RewritesAllContentFiles_PreservingNonContentFiles()
    {
        var inputDir = "/in";
        var outputDir = "/out";
        this.mockFs.AddFile($"{inputDir}/a.content.yaml", new MockFileData(
            "id: a1\nchunks:\n- text: hello\n"));
        this.mockFs.AddFile($"{inputDir}/b.content.yaml", new MockFileData(
            "id: b1\nchunks:\n- text: world\n"));
        // Non-content files should be ignored.
        this.mockFs.AddFile($"{inputDir}/notes.txt", new MockFileData("ignore me"));

        var transformer = new ContentTransformer(inputDir, outputDir, this.mockFs);
        await transformer.TransformAsync(chunk => chunk with { Text = chunk.Text?.ToUpperInvariant() });

        var aOut = this.Fs.GetFile($"{outputDir}/a.content.yaml").OpenText().ReadToEnd();
        var bOut = this.Fs.GetFile($"{outputDir}/b.content.yaml").OpenText().ReadToEnd();
        aOut.Should().Contain("HELLO");
        bOut.Should().Contain("WORLD");
        this.Fs.File.Exists($"{outputDir}/notes.txt").Should().BeFalse();
    }

    [Test]
    public async Task TransformAsync_OutputDirectoryIsCreatedAutomatically()
    {
        this.mockFs.AddFile("/in/a.content.yaml", new MockFileData(
            "chunks:\n- text: hi\n"));

        var transformer = new ContentTransformer("/in", "/missing/output", this.mockFs);
        await transformer.TransformAsync(c => c);

        this.Fs.Directory.Exists("/missing/output").Should().BeTrue();
        this.Fs.File.Exists("/missing/output/a.content.yaml").Should().BeTrue();
    }
}
