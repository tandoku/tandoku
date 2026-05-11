namespace Tandoku.Tests.Subtitles;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Subtitles;

public class SubtitleContentGeneratorTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task GenerateContentAsync_FromWebVtt_ProducesContentBlocks()
    {
        var vtt =
            "WEBVTT\n\n" +
            "00:00:01.000 --> 00:00:02.000\n" +
            "hello\n\n" +
            "00:00:03.000 --> 00:00:04.000\n" +
            "world\n";
        this.mockFs.AddFile("/in/ep.ja.vtt", new MockFileData(vtt));

        var gen = new SubtitleContentGenerator("/in", "/out", this.mockFs);
        await gen.GenerateContentAsync();

        var output = this.Fs.GetFile("/out/ep.content.yaml").OpenText().ReadToEnd();
        output.Should().Contain("hello");
        output.Should().Contain("world");
        output.Should().Contain("ordinal: 1");
        output.Should().Contain("ordinal: 2");
    }

    [Test]
    public async Task GenerateContentAsync_FromSrt_ProducesContentBlocks_StrippingTags()
    {
        // libse's Subtitle.Parse uses the real file system, so this case requires
        // an on-disk input rather than MockFileSystem.
        var tempDir = Path.Combine(Path.GetTempPath(), "tandoku-test-" + Path.GetRandomFileName());
        var inputDir = Path.Combine(tempDir, "in");
        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(inputDir);
        try
        {
            var srt =
                "1\n00:00:01,000 --> 00:00:02,000\n{\\an8}hello world\n\n" +
                "2\n00:00:03,000 --> 00:00:04,000\nsecond line\n";
            await File.WriteAllTextAsync(Path.Combine(inputDir, "ep.ja.srt"), srt);

            var gen = new SubtitleContentGenerator(inputDir, outputDir);
            await gen.GenerateContentAsync();

            var output = await File.ReadAllTextAsync(Path.Combine(outputDir, "ep.content.yaml"));
            output.Should().Contain("hello world");
            output.Should().NotContain("{\\an8}");
            output.Should().Contain("second line");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
