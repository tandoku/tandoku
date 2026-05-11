namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;

public class ContentCommandTests : CliTestBase
{
    [Test]
    public async Task Merge_AlignsByTimecodes()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var refDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("ref");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "source: {timecodes: {start: '00:00:01.000', end: '00:00:02.000'}}\n" +
            "chunks:\n- text: hello\n"));
        this.fileSystem.AddFile(refDir.GetFile("ep.content.yaml"), new MockFileData(
            "source: {timecodes: {start: '00:00:01.000', end: '00:00:02.000'}}\n" +
            "chunks:\n- text: world\n"));

        await this.RunAndAssertAsync(
            $"content merge {inputDir.FullName} {refDir.FullName} {outDir.FullName} --align timecodes --ref english",
            expectedOutput: string.Empty);

        var output = this.fileSystem.GetFile(outDir.GetFile("ep.content.yaml")).TextContents;
        output.Should().Contain("hello");
        output.Should().Contain("world");
        output.Should().Contain("english");
    }

    [Test]
    public async Task Transform_RemoveNonJapaneseText_StripsEnglishOnlyChunks()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "chunks:\n- text: 日本語\n- text: english only\n"));

        await this.RunAndAssertAsync(
            $"content transform remove-non-japanese-text {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        var output = this.fileSystem.GetFile(outDir.GetFile("ep.content.yaml")).TextContents;
        output.Should().Contain("日本語");
        output.Should().NotContain("english only");
    }

    [Test]
    public async Task Transform_RemoveLowConfidenceText_UsesDefaultThreshold()
    {
        // The transform operates on image segments - a content file without image
        // segments should be passed through unchanged.
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "chunks:\n- text: 日本語\n"));

        await this.RunAndAssertAsync(
            $"content transform remove-low-confidence-text {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        this.fileSystem.GetFile(outDir.GetFile("ep.content.yaml")).TextContents
            .Should().Contain("日本語");
    }

    [Test]
    public async Task Transform_MergeRefChunks_PassesThroughEmpty()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        this.fileSystem.AddFile(inputDir.GetFile("ep.content.yaml"), new MockFileData(
            "chunks:\n- text: 日本語\n"));

        await this.RunAndAssertAsync(
            $"content transform merge-ref-chunks {inputDir.FullName} {outDir.FullName}",
            expectedOutput: string.Empty);

        this.fileSystem.GetFile(outDir.GetFile("ep.content.yaml")).TextContents
            .Should().Contain("日本語");
    }

    [Test]
    public async Task Merge_RejectsUnknownAlignmentKind()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var refDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("ref");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        var output = await this.RunAsync(
            $"content merge {inputDir.FullName} {refDir.FullName} {outDir.FullName} --align bogus --ref e");

        output.Result.Should().NotBe(0);
        output.Error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Transform_RemoveNonJapaneseText_RejectsInvalidRole()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        var output = await this.RunAsync(
            $"content transform remove-non-japanese-text {inputDir.FullName} {outDir.FullName} --role bogus-role");

        output.Result.Should().NotBe(0);
        output.Error.Should().NotBeNullOrEmpty();
    }
}
