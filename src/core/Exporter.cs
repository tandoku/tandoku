namespace BlueMarsh.Tandoku;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

public enum ExportFormat
{
    Markdown,
}

public sealed class Exporter
{
    public string Export(string path, string? outPath, ExportFormat format)
    {
        // TODO: optionally infer format from outPath
        return format switch
        {
            ExportFormat.Markdown => new MarkdownExporter().Export(path, outPath),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported format"),
        };
    }

    private sealed class MarkdownExporter
    {
        public string Export(string path, string? outPath)
        {
            var serializer = new TextBlockSerializer();
            var blocks = serializer.Deserialize(path);

            if (outPath is null)
                outPath = Path.ChangeExtension(path, ".md");
            // TODO: support for outPath as directory (share with Importer)

            using var writer = File.CreateText(outPath);

            int footnote = 0;
            foreach (var block in blocks)
            {
                // TODO: skip image if same as previous
                if (block.Image != null)
                {
                    // TODO: this is intended for screenshots, rework this for subtitles and maybe omit for manga (??)
                    if (block.Location != null)
                    {
                        writer.WriteLine($"## {block.Location}");
                        writer.WriteLine();
                    }

                    writer.WriteLine($"![](images/{block.Image.Name})");
                    writer.WriteLine();
                }

                WriteNormalizedMarkdown(writer, block.Text);

                if (!string.IsNullOrWhiteSpace(block.Translation))
                {
                    ++footnote;
                    writer.WriteLine($" [^{footnote}]");
                    writer.Write($"[^{footnote}]: ");

                    // TODO: clean this up
                    WriteNormalizedMarkdown(writer, block.Translation.Replace("\n", "\n    "));
                }

                writer.WriteLine();
                writer.WriteLine();
            }

            return outPath;
        }

        private static void WriteNormalizedMarkdown(TextWriter writer, string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return;

            // Calibre ebook-convert doesn't parse \ in .md files but does work with double-space line ending
            // So we convert backslash line breaks into double-space line breaks
            // (other ideas: export HTML instead of Markdown to use with ebook-convert, or use pandoc instead)
            var doc = Markdown.Parse(s);
            foreach (var para in doc.OfType<ParagraphBlock>())
            {
                foreach (var lineBreak in para.Inline.OfType<LineBreakInline>())
                {
                    if (lineBreak.IsHard)
                        lineBreak.IsBackslash = false;
                }
            }

            doc.WriteTo(writer);
        }
    }
}
