using System.IO;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;

namespace BlueMarsh.Tandoku
{
    internal static class MarkdownExtensions
    {
        internal static string ToMarkdownString(this MarkdownDocument doc, NormalizeOptions? options = null)
        {
            var writer = new StringWriter();
            var renderer = new NormalizeRenderer(writer, options);
            renderer.Render(doc);
            return writer.ToString();
        }
    }
}
