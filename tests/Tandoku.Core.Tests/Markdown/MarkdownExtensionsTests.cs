namespace Tandoku.Tests.Markdown;

public class MarkdownExtensionsTests
{
    private sealed record Md(string? Text) : IMarkdownText;

    [Test]
    public void ToPlainText_StripsMarkdown()
    {
        new Md("**bold** and *em*").ToPlainText().Should().Be($"bold and em{Environment.NewLine}");
    }

    [Test]
    public void ToPlainText_NullText_ReturnsNull()
    {
        new Md(null).ToPlainText().Should().BeNull();
    }

    [Test]
    public void CombineText_FiltersNullsAndWhitespaceAndJoins_Paragraph()
    {
        IMarkdownText?[] items =
        {
            new Md("line one"),
            new Md(""),
            new Md("   "),
            null,
            new Md("line two"),
        };

        var combined = items.CombineText(MarkdownSeparator.Paragraph);
        combined.Text.Should().Be($"line one{Environment.NewLine}{Environment.NewLine}line two");
    }

    [Test]
    public void CombineText_LineBreakSeparator()
    {
        IMarkdownText[] items = { new Md("a"), new Md("b") };
        items.CombineText(MarkdownSeparator.LineBreak).Text
            .Should().Be($"a  {Environment.NewLine}b");
    }

    [Test]
    public void CombineText_SpaceSeparator()
    {
        IMarkdownText[] items = { new Md("a"), new Md("b"), new Md("c") };
        items.CombineText(MarkdownSeparator.Space).Text.Should().Be("a b c");
    }

    [Test]
    public void CombineText_AllEmpty_ReturnsEmpty()
    {
        IMarkdownText[] items = { new Md(null), new Md(""), new Md("  ") };
        items.CombineText(MarkdownSeparator.Paragraph).Text.Should().BeEmpty();
    }

    [Test]
    public void CombineText_UnknownSeparator_Throws()
    {
        IMarkdownText[] items = { new Md("a"), new Md("b") };
        FluentActions.Invoking(() => items.CombineText((MarkdownSeparator)999))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
