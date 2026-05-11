namespace Tandoku.Tests.Subtitles;

using Tandoku.Subtitles;
using Tandoku.Subtitles.WebVtt;

public class WebVttToMarkdownConverterTests
{
    [Test]
    public void PlainText_PassedThrough()
    {
        var spans = new[] { Text("hello") };
        WebVttToMarkdownConverter.Convert(spans).Should().Be("hello");
    }

    [Test]
    public void LineTerminator_BecomesMarkdownLineBreak()
    {
        var spans = new[] { Text("a"), Line(), Text("b") };
        WebVttToMarkdownConverter.Convert(spans)
            .Should().Be($"a  {Environment.NewLine}b");
    }

    [Test]
    public void RubyAnnotation_RendersBaseFollowedByBracketedReading()
    {
        // Base text "日本" with ruby reading "にほん"
        var ruby = new Span
        {
            Type = SpanType.Ruby,
            Children = new[]
            {
                Text("日本"),
                new Span
                {
                    Type = SpanType.RubyText,
                    Children = new[] { Text("にほん") },
                },
            },
        };

        WebVttToMarkdownConverter.Convert(new[] { ruby }).Should().Be("日本[にほん]");
    }

    [Test]
    public void RubyAfterWordCharacter_PrependsSpaceForDisambiguation()
    {
        var spans = new[]
        {
            Text("foo"),
            new Span
            {
                Type = SpanType.Ruby,
                Children = new[]
                {
                    Text("bar"),
                    new Span { Type = SpanType.RubyText, Children = new[] { Text("baz") } },
                },
            },
        };

        WebVttToMarkdownConverter.Convert(spans).Should().Be("foo bar[baz]");
    }

    [Test]
    public void RubyTextWithSpaces_StripsSpacesSilently()
    {
        var ruby = new Span
        {
            Type = SpanType.Ruby,
            Children = new[]
            {
                Text("X"),
                new Span { Type = SpanType.RubyText, Children = new[] { Text("a b c") } },
            },
        };
        WebVttToMarkdownConverter.Convert(new[] { ruby }).Should().Be("X[abc]");
    }

    [Test]
    public void RubyTextWithNonWordChars_Throws()
    {
        var ruby = new Span
        {
            Type = SpanType.Ruby,
            Children = new[]
            {
                Text("X"),
                new Span { Type = SpanType.RubyText, Children = new[] { Text("a-b") } },
            },
        };
        FluentActions.Invoking(() => WebVttToMarkdownConverter.Convert(new[] { ruby }))
            .Should().Throw<InvalidDataException>();
    }

    [Test]
    public void LineTerminatorInsideRuby_Throws()
    {
        var ruby = new Span
        {
            Type = SpanType.Ruby,
            Children = new[]
            {
                Text("X"),
                new Span
                {
                    Type = SpanType.RubyText,
                    Children = new[] { Text("y"), Line() },
                },
            },
        };
        FluentActions.Invoking(() => WebVttToMarkdownConverter.Convert(new[] { ruby }))
            .Should().Throw<InvalidDataException>();
    }

    private static Span Text(string s) => new() { Type = SpanType.Text, Text = s };
    private static Span Line() => new() { Type = SpanType.LineTerminator };
}
