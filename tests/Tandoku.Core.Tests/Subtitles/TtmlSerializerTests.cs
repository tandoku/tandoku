namespace Tandoku.Tests.Subtitles;

using System.Text;
using Tandoku.Subtitles.Ttml;

public class TtmlSerializerTests
{
    [Test]
    public async Task DeserializeAsync_ParsesEmbeddedAmazonSample()
    {
        using var stream = this.GetType().GetManifestResourceStream("Amazon1.ttml");
        var doc = await TtmlSerializer.DeserializeAsync(stream);

        doc.Should().NotBeNull();
        doc.Body.Should().NotBeNull();
        doc.Body!.Divs.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task DeserializeAsync_ParsesEmbeddedNetflixSample()
    {
        using var stream = this.GetType().GetManifestResourceStream("Netflix1.ttml");
        var doc = await TtmlSerializer.DeserializeAsync(stream);

        doc.Should().NotBeNull();
        doc.Body.Should().NotBeNull();
    }

    [Test]
    public void Deserialize_FromMemoryStream_UsesSyncPath()
    {
        // Synchronous overload; constructed inline so we don't rely on resources.
        var xml =
            """
            <tt xmlns="http://www.w3.org/ns/ttml" xml:lang="ja">
              <body>
                <div>
                  <p begin="00:00:01.000" end="00:00:02.000">hello</p>
                </div>
              </body>
            </tt>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var doc = TtmlSerializer.Deserialize(stream);

        doc.Language.Should().Be("ja");
        var p = doc.Body!.Divs![0].Paragraphs![0];
        p.Begin.Should().Be(TimeSpan.FromSeconds(1));
        p.End.Should().Be(TimeSpan.FromSeconds(2));
        p.Content!.OfType<string>().Should().Contain("hello");
    }

    [Test]
    public void Deserialize_OffsetTimeExpressions_AreParsed()
    {
        var xml =
            """
            <tt xmlns="http://www.w3.org/ns/ttml">
              <body>
                <div>
                  <p begin="500ms" end="1.5s">x</p>
                </div>
              </body>
            </tt>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var doc = TtmlSerializer.Deserialize(stream);
        var p = doc.Body!.Divs![0].Paragraphs![0];
        p.Begin.Should().Be(TimeSpan.FromMilliseconds(500));
        p.End.Should().Be(TimeSpan.FromSeconds(1.5));
    }

    [Test]
    public void Deserialize_FrameMetric_NotSupported()
    {
        var xml =
            """
            <tt xmlns="http://www.w3.org/ns/ttml">
              <body><div><p begin="10f" end="20f">x</p></div></body>
            </tt>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        FluentActions.Invoking(() => TtmlSerializer.Deserialize(stream))
            .Should().Throw<Exception>();
    }

    [Test]
    public void Deserialize_RubyAttribute_RoundTripsToEnum()
    {
        var xml =
            """
            <tt xmlns="http://www.w3.org/ns/ttml" xmlns:tts="http://www.w3.org/ns/ttml#styling">
              <head>
                <styling>
                  <style xml:id="r1" tts:ruby="text"/>
                  <style xml:id="r2" tts:ruby="base"/>
                </styling>
              </head>
            </tt>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var doc = TtmlSerializer.Deserialize(stream);
        var styles = doc.Head!.Styling!.Styles!;
        styles.Should().HaveCount(2);
        styles[0].Ruby.Should().Be(TtmlRuby.Text);
        styles[1].Ruby.Should().Be(TtmlRuby.Base);
        styles[0].RubyString.Should().Be("text");
    }
}
