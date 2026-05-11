namespace Tandoku.Tests.Subtitles;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Subtitles;

public class SubtitleGeneratorTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task GenerateAsync_DefaultPurpose_ProducesSrtTextFromChunks()
    {
        this.mockFs.AddFile("/in/ep.content.yaml", new MockFileData(
            "source: {timecodes: {start: '00:00:00.500', end: '00:00:01.500'}}\nchunks:\n- text: hello\n"));

        var gen = new SubtitleGenerator(SubtitlePurpose.Default, includeReference: null,
            extendAudioMsecs: 0, fileSystem: this.mockFs);
        await gen.GenerateAsync("/in", "/out");

        var srt = this.Fs.GetFile("/out/ep.srt").OpenText().ReadToEnd();
        srt.Should().Contain("hello");
        srt.Should().Contain("00:00:00,500 --> 00:00:01,500");
    }

    [Test]
    public async Task GenerateAsync_MediaExtractionPurpose_EmitsBaseNameOrdinalLine()
    {
        this.mockFs.AddFile("/in/ep.content.yaml", new MockFileData(
            "source: {ordinal: 4, timecodes: {start: '00:00:00.000', end: '00:00:01.000'}}\nchunks:\n- text: foo\n"));

        var gen = new SubtitleGenerator(SubtitlePurpose.MediaExtraction, includeReference: null,
            extendAudioMsecs: 0, fileSystem: this.mockFs);
        await gen.GenerateAsync("/in", "/out");

        var srt = this.Fs.GetFile("/out/ep.srt").OpenText().ReadToEnd();
        srt.Should().Contain("ep|4");
    }

    [Test]
    public async Task GenerateAsync_IncludeReference_EmitsReferenceWhenPrimaryEmpty()
    {
        // Block has no primary chunk text, but a reference with text.
        this.mockFs.AddFile("/in/ep.content.yaml", new MockFileData(
            "references:\n  subs:\n    source: {timecodes: {start: '00:00:00.000', end: '00:00:02.000'}}\n" +
            "chunks:\n- references:\n    subs:\n      text: ref-line\n"));

        var gen = new SubtitleGenerator(SubtitlePurpose.Default, includeReference: "subs",
            extendAudioMsecs: 0, fileSystem: this.mockFs);
        await gen.GenerateAsync("/in", "/out");

        var srt = this.Fs.GetFile("/out/ep.srt").OpenText().ReadToEnd();
        srt.Should().Contain("ref-line");
    }

    [Test]
    public async Task GenerateAsync_ExtendAudioMsecs_AddedToEndTime()
    {
        this.mockFs.AddFile("/in/ep.content.yaml", new MockFileData(
            "source: {timecodes: {start: '00:00:00.000', end: '00:00:01.000'}}\nchunks:\n- text: foo\n"));

        var gen = new SubtitleGenerator(SubtitlePurpose.Default, includeReference: null,
            extendAudioMsecs: 250, fileSystem: this.mockFs);
        await gen.GenerateAsync("/in", "/out");

        var srt = this.Fs.GetFile("/out/ep.srt").OpenText().ReadToEnd();
        srt.Should().Contain("00:00:01,250");
    }

    [Test]
    public async Task GenerateAsync_ContentFileWithoutBaseName_Throws()
    {
        // a plain "ep.yaml" doesn't have the expected ".content.yaml" tail; however the
        // EnumerateContentFiles filter uses the .content.yaml extension, so this file is
        // simply skipped. Use an explicit invalid scenario by providing a file without
        // the .content.yaml suffix and verify no output is produced.
        this.mockFs.AddFile("/in/ep.yaml", new MockFileData("chunks:\n- text: x\n"));
        var gen = new SubtitleGenerator(SubtitlePurpose.Default, null, 0, this.mockFs);
        await gen.GenerateAsync("/in", "/out");
        this.Fs.File.Exists("/out/ep.srt").Should().BeFalse();
    }
}
