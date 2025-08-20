namespace Tandoku.Tests.Subtitles;

using Tandoku.Subtitles;
using Tandoku.Subtitles.WebVtt;

public class TtmlToWebVttConverterTests
{
    [Fact]
    public async Task ConvertAmazonSubtitle()
    {
        var targetStream = await this.ConvertToWebVttStreamAsync("SampleAmazon.ja.ttml");
        await Verify(targetStream, "vtt");
    }

    private async Task<MemoryStream> ConvertToWebVttStreamAsync(string resourceName)
    {
        // TODO - utility method to get manifest resource stream
        var ttmlStream = this.GetType().Assembly.GetManifestResourceStream(this.GetType(), resourceName);
        ttmlStream.Should().NotBeNull($"Missing resource stream: {resourceName}");
        var targetDoc = await TtmlToWebVttConverter.ConvertAsync(ttmlStream!);
        var targetStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(targetStream, leaveOpen: true))
            await WebVttSerializer.SerializeAsync(targetDoc, streamWriter);
        targetStream.Position = 0;
        return targetStream;
    }
}
