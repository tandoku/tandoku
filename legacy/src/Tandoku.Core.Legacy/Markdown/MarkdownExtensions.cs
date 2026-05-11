namespace Tandoku;

using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;

public interface IMarkdownText
{
    string? Text { get; }
}

public enum MarkdownSeparator
{
    Paragraph,
    LineBreak,
    Space,
}

internal static class MarkdownExtensions
{
    public static string? ToPlainText(this IMarkdownText markdownText) =>
        markdownText.Text is not null ? Markdown.ToPlainText(markdownText.Text) : null;

    public static IMarkdownText CombineText(this IEnumerable<IMarkdownText?> markdownTexts, MarkdownSeparator separator) =>
        new MarkdownText(string.Join(
            SepToString(separator),
            markdownTexts.Where(t => !string.IsNullOrWhiteSpace(t?.Text)).Select(t => t!.Text)));

    internal static string ToMarkdownString(this MarkdownObject md, NormalizeOptions? options = null)
    {
        var writer = new StringWriter();
        WriteTo(md, writer, options);
        return writer.ToString();
    }

    internal static void WriteTo(this MarkdownObject md, TextWriter writer, NormalizeOptions? options = null)
    {
        var renderer = new NormalizeRenderer(writer, options);
        renderer.Render(md);
    }

    private static string SepToString(MarkdownSeparator separator) => separator switch
    {
        MarkdownSeparator.Paragraph => $"{Environment.NewLine}{Environment.NewLine}",
        MarkdownSeparator.LineBreak => $"  {Environment.NewLine}",
        MarkdownSeparator.Space => " ",
        _ => throw new ArgumentOutOfRangeException(nameof(separator)),
    };

    private sealed record MarkdownText(string? Text) : IMarkdownText;
}
