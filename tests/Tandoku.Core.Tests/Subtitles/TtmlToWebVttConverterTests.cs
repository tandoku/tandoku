namespace Tandoku.Tests.Subtitles;

using Tandoku.Subtitles;
using Tandoku.Subtitles.WebVtt;

public class TtmlToWebVttConverterTests
{
    [Fact]
    public Task ConvertAmazonSubtitle() => this.TestConversionAsync("Amazon1.ttml");

    [Fact]
    public Task ConvertNetflixSubtitle() => this.TestConversionAsync("Netflix1.ttml");

    private async Task TestConversionAsync(string resourceName)
    {
        var ttmlStream = this.GetType().GetManifestResourceStream(resourceName);
        var targetDoc = await TtmlToWebVttConverter.ConvertAsync(ttmlStream);

        var targetStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(targetStream, leaveOpen: true))
            await WebVttSerializer.SerializeAsync(targetDoc, streamWriter);

        await Verify(targetStream, "vtt");
    }
}
