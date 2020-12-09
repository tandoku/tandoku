using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SubtitlesParser.Classes;

namespace BlueMarsh.Tandoku
{
    public sealed class Importer
    {
        public string Import(string path)
        {
            var textBlocks = Path.GetExtension(path).ToUpperInvariant() switch
            {
                ".MD" => new MarkdownImporter().Import(path),
                ".ASS" => new SubtitleImporter().Import(path),
                _ => throw new ArgumentException($"Unsupported file type: {path}"),
            };
            var outPath = Path.ChangeExtension(path, ".tdkc.jsonl");
            var serializer = new TextBlockSerializer();
            serializer.Serialize(outPath, textBlocks);
            return outPath;
        }

        private sealed class MarkdownImporter
        {
            public IEnumerable<TextBlock> Import(string path)
            {
                var doc = Markdown.Parse(File.ReadAllText(path));
                foreach (var para in doc.OfType<ParagraphBlock>())
                {
                    foreach (var literal in para.Inline.OfType<LiteralInline>())
                    {
                        yield return new TextBlock { Text = literal.Content.ToString() };
                    }
                }
            }
        }

        private sealed class SubtitleImporter
        {
            public IEnumerable<TextBlock> Import(string path)
            {
                var subtitleItems = ParseSubtitleItems(path);

                var itemsEnum = subtitleItems.GetEnumerator();

                while (itemsEnum.MoveNext())
                {
                    var text = GetTextFromItem(itemsEnum.Current);
                    var times = (itemsEnum.Current.StartTime, itemsEnum.Current.EndTime);

                    string? translation = null;
                    if (itemsEnum.MoveNext() && times == (itemsEnum.Current.StartTime, itemsEnum.Current.EndTime))
                    {
                        translation = GetTextFromItem(itemsEnum.Current);
                    }

                    yield return new TextBlock
                    {
                        Text = text,
                        Translation = translation,
                        Location = TimeSpan.FromMilliseconds(times.StartTime).ToString("g")
                    };
                }
            }

            private string GetTextFromItem(SubtitleItem item)
            {
                // TODO: this should be fixed in the SubtitlesParser package (there's a TODO in SsaParser)
                // (send a PR for this and additionally add Actor, Style fields from SSA format; fork and build BlueMarsh copy of SubtitlesParser for now ??)
                // -OR- try Ass-Loader library instead
                if (item.Lines.Count == 1)
                {
                    var lines = item.Lines[0].Split(@"\N");
                    item.Lines.Clear();
                    item.Lines.AddRange(lines);
                }

                var doc = new MarkdownDocument();
                foreach (var line in item.Lines)
                {
                    var inline = new ContainerInline();
                    inline.AppendChild(new LiteralInline(line));
                    doc.Add(new ParagraphBlock { Inline = inline });
                }
                var writer = new StringWriter();
                var renderer = new Markdig.Renderers.Normalize.NormalizeRenderer(writer);
                renderer.Render(doc);
                return writer.ToString();
            }

            private static List<SubtitleItem> ParseSubtitleItems(string path)
            {
                var parser = new SubtitlesParser.Classes.Parsers.SsaParser();
                using (var stream = File.OpenRead(path))
                    return parser.ParseStream(stream, Encoding.UTF8);
            }
        }
    }
}
