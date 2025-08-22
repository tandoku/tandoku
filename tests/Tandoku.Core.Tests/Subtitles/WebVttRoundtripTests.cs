namespace Tandoku.Tests.Subtitles;

using Tandoku.Subtitles.WebVtt;

public class WebVttRoundtripTests
{
    // TODO - check that output actually matches the original
    // separately from Verify() or can be integrated into it?

    [Fact]
    public Task SampleCaption() => this.TestRoundtripWebVttAsync("SampleCaption.vtt");

    [Fact]
    public Task SampleChapters() => this.TestRoundtripWebVttAsync("SampleChapters.vtt");


    [Fact]
    public Task SampleMetadata() => this.TestRoundtripWebVttAsync("SampleMetadata.vtt");

    [Fact]
    public Task SampleRegions() => this.TestRoundtripWebVttAsync("SampleRegions.vtt");

    [Fact]
    public Task SampleRuby() => this.TestRoundtripWebVttAsync("SampleRuby.vtt");

    [Fact]
    public Task Netflix2() => this.TestRoundtripWebVttAsync("Netflix2.vtt");

    private async Task TestRoundtripWebVttAsync(string resourceName)
    {
        var inputStream = this.GetType().GetManifestResourceStream(resourceName);
        var doc = await WebVttParser.ReadAsync(new StreamReader(inputStream));

        // TODO - share with TtmlToWebVttConverterTests
        var targetStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(targetStream, leaveOpen: true))
            await WebVttSerializer.SerializeAsync(doc, streamWriter);

        await Verify(targetStream, "vtt");
    }
}
