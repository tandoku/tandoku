namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;

public class SubtitlesCommandTests : CliTestBase
{
    [Test]
    public async Task GenerateContent_FromWebVtt()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");
        var vtt =
            "WEBVTT\n\n" +
            "00:00:01.000 --> 00:00:02.000\n" +
            "hello\n";
        this.fileSystem.AddFile(inputDir.GetFile("ep.ja.vtt"), new MockFileData(vtt));

        await this.RunAndAssertAsync(
            $"subtitles generate-content {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        var output = this.fileSystem.GetFile(outDir.GetFile("ep.content.yaml")).TextContents;
        output.Should().Contain("hello");
    }

    [Test]
    public async Task Generate_ProducesSrtFromContent()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");
        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "source: {timecodes: {start: '00:00:00.500', end: '00:00:01.500'}}\n" +
            "chunks:\n- text: hello\n"));

        await this.RunAndAssertAsync(
            $"subtitles generate {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        var output = this.fileSystem.GetFile(outDir.GetFile("ep.srt")).TextContents;
        output.Should().Contain("hello");
        output.Should().Contain("00:00:00,500 --> 00:00:01,500");
    }

    [Test]
    public async Task Generate_WithExtendAudioOption_BumpsEndTime()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");
        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "source: {timecodes: {start: '00:00:00.000', end: '00:00:01.000'}}\n" +
            "chunks:\n- text: foo\n"));

        await this.RunAndAssertAsync(
            $"subtitles generate {inputDir.FullName} {outDir.FullName} --extend-audio 250",
            expectedOutput: string.Empty);

        this.fileSystem.GetFile(outDir.GetFile("ep.srt")).TextContents
            .Should().Contain("00:00:01,250");
    }

    [Test]
    public async Task TtmlToWebVtt_ConvertsFiles()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");
        var ttml =
            """
            <tt xmlns="http://www.w3.org/ns/ttml">
              <body>
                <div>
                  <p begin="00:00:01.000" end="00:00:02.000">hello</p>
                </div>
              </body>
            </tt>
            """;
        this.fileSystem.AddFile(inputDir.GetFile("ep.ttml"), new MockFileData(ttml));

        await this.RunAndAssertAsync(
            $"subtitles ttml-to-webvtt {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        var output = this.fileSystem.GetFile(outDir.GetFile("ep.vtt")).TextContents;
        output.Should().Contain("WEBVTT");
        output.Should().Contain("hello");
    }
}
