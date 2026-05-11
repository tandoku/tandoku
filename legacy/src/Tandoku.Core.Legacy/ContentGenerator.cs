namespace Tandoku;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SubtitlesParser.Classes;
using WanaKanaSharp;

public enum ContentGeneratorInputType
{
    ImageText,
    Markdown,
    Subtitles,
}

public enum ContentOutputBehavior
{
    None,
    Append,
    Overwrite,
}

public sealed class ContentGenerator
{
    private const string DefaultExtension = ".tdkc.yaml";

    // TODO: change inputPaths/outPath to FileSystemInfo/FileInfo
    public string Generate(
        IEnumerable<string> inputPaths,
        ContentGeneratorInputType? inputType = null,
        string? outPath = null,
        ContentOutputBehavior contentOutputBehavior = ContentOutputBehavior.None)
    {
        var inferredInputType = inputType ?? DetectInputType(inputPaths);
        var generator = GetContentGenerator(inferredInputType);
        var expandedInputPaths = inputPaths; // TODO: FileStoreUtil.ExpandPaths(inputPaths);
        var textBlocks = generator.GenerateContent(expandedInputPaths);

        //TODO: implement outPath inference
        //if (outPath is null)
        //{
        //    outPath = TryGetFileNameFromDirectory(path, DefaultExtension, out var outFileName) ?
        //        Path.Join(path, outFileName) :
        //        Path.ChangeExtension(path, DefaultExtension);
        //}
        //else if (TryGetFileNameFromDirectory(outPath, DefaultExtension, out var outFileName))
        //{
        //    outPath = Path.Join(outPath, outFileName);
        //}
        // TODO: outPath still could be invalid (unknown extension or directory that doesn't exist)

        var serializer = new TextBlockSerializer();
        serializer.Serialize(outPath, textBlocks);
        return outPath;
    }

    private static ContentGeneratorInputType DetectInputType(IEnumerable<string> inputPaths)
    {
        var extensions = inputPaths
            .Select(p => Path.GetExtension(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extension = extensions.Count == 1 ?
            extensions.Single() :
            throw new ArgumentException($"Cannot determine input type from the specified extensions '{string.Join(", ", extensions)}'.");

        return extension.ToUpperInvariant() switch
        {
            ".JPG" => ContentGeneratorInputType.ImageText,
            ".JPEG" => ContentGeneratorInputType.ImageText,
            ".PNG" => ContentGeneratorInputType.ImageText,
            ".MD" => ContentGeneratorInputType.Markdown,
            ".ASS" => ContentGeneratorInputType.Subtitles,
            ".SRT" => ContentGeneratorInputType.Subtitles,
            ".VTT" => ContentGeneratorInputType.Subtitles,
            _ => throw new ArgumentException($"Cannot determine input type from the specified extension '{extension}'."),
        };
    }

    private static IContentGenerator GetContentGenerator(ContentGeneratorInputType inputType)
    {
        return inputType switch
        {
            ContentGeneratorInputType.ImageText => new ImageTextContentGenerator(),
            ContentGeneratorInputType.Markdown => new MarkdownContentGenerator(),
            ContentGeneratorInputType.Subtitles => new SubtitleContentGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(inputType)),
        };
    }

    private static bool TryGetFileNameFromDirectory(
        string path,
        string extension,
        [NotNullWhen(true)] out string? fileName)
    {
        if (Directory.Exists(path))
        {
            fileName = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
                + ".tdkc.yaml";
            return true;
        }
        fileName = null;
        return false;
    }

    private interface IContentGenerator
    {
        IEnumerable<TextBlock> GenerateContent(IEnumerable<string> inputPaths);
    }

    private sealed class ImageTextContentGenerator : IContentGenerator
    {
        private static readonly string DoubleLineBreak = string.Concat(Environment.NewLine, Environment.NewLine);

        public IEnumerable<TextBlock> GenerateContent(IEnumerable<string> inputPaths)
        {
            var imageTextJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            int imageNumber = 0;

            foreach (var imagePath in inputPaths)
            {
                var imageName = Path.GetFileName(imagePath);
                imageNumber++;

                var textBlock = new TextBlock
                {
                    Image = new Image { Name = imageName },
                    Location = $"#{imageNumber} - {Path.GetFileNameWithoutExtension(imageName)}",
                };

                var imageTextPath = Path.Join(
                    Path.GetDirectoryName(imagePath),
                    "text",
                    Path.GetFileNameWithoutExtension(imagePath) + ".acv.json");

                if (File.Exists(imageTextPath))
                {
                    var imageText = JsonSerializer.Deserialize<ImageTextData>(
                        File.ReadAllText(imageTextPath),
                        imageTextJsonOptions);

                    var imageMap = new ImageMap
                    {
                        // TODO: move filtering into transforms
                        Lines = FilterLines(imageText.AnalyzeResult.ReadResults.Single().Lines.Select(l => new ImageMapLine
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

                    if (imageMap.Lines.Any())
                    {
                        textBlock.Image.Map = imageMap;
                        textBlock.Text = string.Join(
                            DoubleLineBreak,
                            imageMap.Lines.Select(l => l.Text));
                    }
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
                if (GetLineHeight(line) <= furiganaLineHeight &&
                    line.Text?.Any(WanaKana.IsKana) == true &&
                    line.Text?.All(IsAllowedForFurigana) == true)
                {
                    continue;
                }

                yield return line;
            }
        }

        private static int GetLineHeight(ImageMapLine line) => line.ToRectangle().Height;
        private static bool IsAllowedForFurigana(char c) =>
            WanaKana.IsKana(c) ||
            char.IsWhiteSpace(c) ||
            WanaKana.IsJapanesePunctuation(c) ||
            WanaKana.IsEnglishPunctuation(c);

        private record ImageTextData(AnalyzeResult AnalyzeResult);
        private record AnalyzeResult(List<ReadResult> ReadResults);
        private record ReadResult(List<ReadResultLine> Lines);
        private record ReadResultLine(int[] BoundingBox, string Text, List<ReadResultWord> Words);
        private record ReadResultWord(int[] BoundingBox, string Text, double Confidence);
    }

    private sealed class MarkdownContentGenerator : IContentGenerator
    {
        public IEnumerable<TextBlock> GenerateContent(IEnumerable<string> inputPaths)
        {
            //var markdownPipeline = new MarkdownPipelineBuilder()
            //    .UseFootnotes()
            //    .Build();
            //var doc = Markdown.Parse(File.ReadAllText(path), markdownPipeline);

            foreach (var inputPath in inputPaths)
            {
                var doc = Markdown.Parse(File.ReadAllText(inputPath));
                foreach (var para in doc.OfType<ParagraphBlock>())
                {
                    foreach (var literal in para.Inline.OfType<LiteralInline>())
                    {
                        yield return new TextBlock { Text = literal.Content.ToString() };
                    }
                }
            }
        }
    }

    private sealed class SubtitleContentGenerator : IContentGenerator
    {
        public IEnumerable<TextBlock> GenerateContent(IEnumerable<string> inputPaths)
        {
            foreach (var inputPath in inputPaths)
            {
                var subtitleItems = ParseSubtitleItems(inputPath);

                var itemsEnum = subtitleItems.GetEnumerator();

                while (itemsEnum.MoveNext())
                {
                    var text = GetTextFromItem(itemsEnum.Current);
                    var times = (itemsEnum.Current.StartTime, itemsEnum.Current.EndTime);

                    string? translation = null;
                    // this was used for importing from subs2srs aligned subtitles; move elsewhere or delete
                    //if (itemsEnum.MoveNext() && times == (itemsEnum.Current.StartTime, itemsEnum.Current.EndTime))
                    //{
                    //    translation = GetTextFromItem(itemsEnum.Current);
                    //}

                    yield return new TextBlock
                    {
                        Text = text,
                        Translation = translation,
                        Location = TimeSpan.FromMilliseconds(times.StartTime).ToString("g"),
                        Source = new BlockSource
                        {
                            Timecodes = new TimecodePair
                            {
                                Start = TimeSpan.FromMilliseconds(times.StartTime),
                                End = TimeSpan.FromMilliseconds(times.EndTime),
                            },
                        },
                    };
                }
            }
        }

        private static string GetTextFromItem(SubtitleItem item)
        {
            // TODO: this should be fixed in the SubtitlesParser package (there's a TODO in SsaParser)
            // (send a PR for this and additionally add Actor, Style fields from SSA format; fork and build tandoku copy of SubtitlesParser for now ??)
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
            var parser = new SubtitlesParser.Classes.Parsers.SubParser();
            using (var stream = File.OpenRead(path))
                return parser.ParseStream(stream, Encoding.UTF8);
        }
    }
}
