namespace Tandoku.Tests.Subtitles;

using Tandoku.Subtitles.WebVtt;

public class WebVttRoundtripTests
{
    // TODO - check that output actually matches the original
    // separately from Verify() or can be integrated into it?

    [Fact]
    public async Task SampleCaption()
    {
        var targetStream = await this.RoundtripWebVttStreamAsync("SampleCaption.vtt");
        await Verify(targetStream, "vtt");
    }

    [Fact]
    public async Task SampleRuby()
    {
        var targetStream = await this.RoundtripWebVttStreamAsync("SampleRuby.vtt");
        await Verify(targetStream, "vtt");
    }

    private async Task<MemoryStream> RoundtripWebVttStreamAsync(string resourceName)
    {
        var inputStream = this.GetType().GetManifestResourceStream(resourceName);
        var doc = await WebVttParser.ReadAsync(new StreamReader(inputStream));

        // TODO - share with TtmlToWebVttConverterTests
        var targetStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(targetStream, leaveOpen: true))
            await WebVttSerializer.SerializeAsync(doc, streamWriter);
        targetStream.Position = 0;

        return targetStream;
    }
}
