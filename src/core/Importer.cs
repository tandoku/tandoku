using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SubtitlesParser.Classes;
using WanaKanaSharp;

namespace BlueMarsh.Tandoku
{
    public sealed class Importer
    {
        public string Import(string path, bool images = false)
        {
            // TODO: when importing images, select a real filename for outPath

            IContentImporter importer = images ? new ImagesImporter() :
                Path.GetExtension(path).ToUpperInvariant() switch
                {
                    ".MD" => new MarkdownImporter(),
                    ".ASS" => new SubtitleImporter(),
                    _ => throw new ArgumentException($"Unsupported file type: {path}"),
                };
            var textBlocks = importer.Import(path);
            var outPath = Path.ChangeExtension(path, ".tdkc.jsonl");
            var serializer = new TextBlockSerializer();
            serializer.Serialize(outPath, textBlocks);
            return outPath;
        }

        private interface IContentImporter
        {
            IEnumerable<TextBlock> Import(string path);
        }

        private sealed class ImagesImporter : IContentImporter
        {
            public IEnumerable<TextBlock> Import(string path)
            {
                var ocrJsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                var imagesPath = Path.Combine(path, "images");
                int imageNumber = 0;

                foreach (var imagePath in Directory.EnumerateFiles(imagesPath))
                {
                    var imageName = Path.GetFileName(imagePath);
                    imageNumber++;

                    var textBlock = new TextBlock
                    {
                        Image = new Image { Name = imageName },
                        Location = $"#{imageNumber} - {Path.GetFileNameWithoutExtension(imageName)}",
                    };

                    var ocrPath = Path.Combine(
                        imagesPath,
                        "ocr",
                        Path.GetFileNameWithoutExtension(imagePath) + ".acv.json");
                    if (File.Exists(ocrPath))
                    {
                        var ocr = JsonSerializer.Deserialize<OcrData>(File.ReadAllText(ocrPath), ocrJsonOptions);
                        textBlock.Image.Map = new ImageMap
                        {
                            Lines = FilterLines(ocr.AnalyzeResult.ReadResults.Single().Lines.Select(l => new ImageMapLine
                            {
                                BoundingBox = l.BoundingBox,
                                Text = l.Text,
                                Words = l.Words.Select(w => new ImageMapWord
                                {
                                    BoundingBox = w.BoundingBox,
                                    Text = w.Text,
                                    Confidence = w.Confidence,
                                }).ToList(),
                            })).ToList(),
                        };

                        textBlock.Text = string.Join(
                            "\n\n",
                            textBlock.Image.Map.Lines.Select(l => l.Text));
                    }

                    yield return textBlock;
                }
            }

            private IEnumerable<ImageMapLine> FilterLines(IEnumerable<ImageMapLine> lines)
            {
                var filteredLines = new List<ImageMapLine>();

                foreach (var line in lines)
                {
                    // Exclude low-confidence lines
                    if (line.Words.All(w => w.Confidence != null && w.Confidence < 0.5))
                        continue;

                    // Exclude lines with no Japanese characters
                    if (line.Text?.Any(c => WanaKana.IsKana(c) || WanaKana.IsKanji(c)) == false)
                        continue;

                    filteredLines.Add(line);
                }

                // TODO: move furigana processing after block clustering
                var furiganaLineHeight = filteredLines.Count > 0 ? filteredLines.Max(GetLineHeight) * 0.7 : 0;

                foreach (var line in filteredLines)
                {
                    // Exclude furigana lines (TODO: should happen after block clustering/line reordering, based on comparison to next line)
                    if (GetLineHeight(line) <= furiganaLineHeight && line.Text?.All(IsAllowedForFurigana) == true)
                        continue;

                    yield return line;
                }
            }

            private static int GetLineHeight(ImageMapLine line) => line.ToRectangle().Height;
            private static bool IsAllowedForFurigana(char c) =>
                WanaKana.IsKana(c) ||
                char.IsWhiteSpace(c) ||
                WanaKana.IsJapanesePunctuation(c) ||
                WanaKana.IsEnglishPunctuation(c);

            private record OcrData(AnalyzeResult AnalyzeResult);
            private record AnalyzeResult(List<ReadResult> ReadResults);
            private record ReadResult(List<ReadResultLine> Lines);
            private record ReadResultLine(int[] BoundingBox, string Text, List<ReadResultWord> Words);
            private record ReadResultWord(int[] BoundingBox, string Text, double Confidence);
        }

        private sealed class MarkdownImporter : IContentImporter
        {
            public IEnumerable<TextBlock> Import(string path)
            {
                //var markdownPipeline = new MarkdownPipelineBuilder()
                //    .UseFootnotes()
                //    .Build();
                //var doc = Markdown.Parse(File.ReadAllText(path), markdownPipeline);

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

        private sealed class SubtitleImporter : IContentImporter
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

            private static string GetTextFromItem(SubtitleItem item)
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
                var inline = new ContainerInline();
                foreach (var line in item.Lines)
                {
                    if (inline.FirstChild != null)
                    {
                        // Use backslash line breaks (rather than double-space line ending) because trailing spaces
                        // make the default YAML ugly as it ends up using "" with \n line breaks
                        inline.AppendChild(new LineBreakInline
                        {
                            IsBackslash = true,
                            IsHard = true
                        });
                    }

                    inline.AppendChild(new LiteralInline(line));
                }
                doc.Add(new ParagraphBlock { Inline = inline });

                return doc.ToMarkdownString();
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
