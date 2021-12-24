namespace Tandoku;

using Markdig.Renderers.Normalize;
using Markdig.Syntax;

internal static class MarkdownExtensions
{
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
