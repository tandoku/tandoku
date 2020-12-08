using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlueMarsh.Tandoku
{
    public sealed class Exporter
    {
        public string Export(string path, ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Markdown => new MarkdownExporter().Export(path),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported format"),
            };
        }

        private sealed class MarkdownExporter
        {
            public string Export(string path)
            {
                var serializer = new TextBlockSerializer();
                var blocks = serializer.Deserialize(path);

                var outPath = Path.ChangeExtension(path, ".md");
                using var writer = File.CreateText(outPath);

                int footnote = 0;
                foreach (var block in blocks)
                {
                    writer.Write(block.Text);

                    if (!string.IsNullOrEmpty(block.Translation))
                    {
                        ++footnote;
                        writer.WriteLine($" [^{footnote}]");
                        writer.Write($"[^{footnote}]: ");

                        // TODO: clean this up
                        writer.Write(block.Translation.Replace("\n", "\n    "));
                    }

                    writer.WriteLine();
                    writer.WriteLine();
                }

                return outPath;
            }
        }
    }

    public enum ExportFormat
    {
        Markdown,
    }
}
