namespace Tandoku;

using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;

public interface IMarkdownText
{
    string? Text { get; }
}

internal static class MarkdownExtensions
{
    public static string? ToPlainText(this IMarkdownText markdownText) =>
        markdownText.Text is not null ? Markdown.ToPlainText(markdownText.Text) : null;

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
}
