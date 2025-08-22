namespace Tandoku.Subtitles;

using System.IO.Abstractions;
using Tandoku.Subtitles.Ttml;
using Tandoku.Subtitles.WebVtt;

/// <summary>
/// Converter for TTML subtitles to WebVTT format. Preserves ruby annotations.
/// </summary>
/// <remarks>
/// An in-depth description of how to map TTML to WebVTT can be found here:
/// https://w3c.github.io/ttml-webvtt-mapping/
/// However, at least currently, it does not specifically cover ruby text.
/// </remarks>
public sealed class TtmlToWebVttConverter
{
    private readonly IFileSystem fileSystem;
    private readonly string inputPath;
    private readonly string outputPath;

    public TtmlToWebVttConverter(string inputPath, string outputPath, IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.inputPath = inputPath;
        this.outputPath = outputPath;
    }

    public async Task ConvertAsync()
    {
        var inputDir = this.fileSystem.GetDirectory(this.inputPath);
        var outputDir = this.fileSystem.GetDirectory(this.outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateTtmlSubtitleFiles())
        {
            var baseName = this.fileSystem.Path.GetFileNameWithoutExtension(inputFile.Name);
            var targetName = this.fileSystem.Path.ChangeExtension(baseName, SubtitleExtensions.WebVtt);
            var outputFile = outputDir.GetFile(targetName);
            var webVttDocument = await ConvertAsync(inputFile.OpenRead());
            using var outputWriter = outputFile.CreateText();
            await WebVttSerializer.SerializeAsync(webVttDocument, outputWriter);
        }
    }

    public static async Task<WebVttDocument> ConvertAsync(Stream stream)
    {
        var ttmlDocument = await TtmlSerializer.DeserializeAsync(stream);
        return Convert(ttmlDocument);
    }

    public static WebVttDocument Convert(TtmlDocument ttmlDocument)
    {
        var visitor = new ConvertVisitor();
        visitor.VisitDocument(ttmlDocument);
        return visitor.Target;
    }

    private sealed class ConvertVisitor : TtmlDocumentVisitor
    {
        private readonly WebVttDocument targetDoc = new();
        private readonly List<Cue> cues = [];
        private readonly List<Span> spans = [];

        internal WebVttDocument Target => this.targetDoc;

        public override void VisitDocument(TtmlDocument document)
        {
            base.VisitDocument(document);

            this.targetDoc.Cues = [.. this.cues];

            this.cues.Clear();
        }

        public override void VisitParagraph(TtmlParagraph paragraph)
        {
            var cue = new Cue
            {
                Start = paragraph.Begin,
                End = paragraph.End
            };

            base.VisitParagraph(paragraph);

            cue.Content = [.. this.spans];

            this.cues.Add(cue);
            this.spans.Clear();
        }

        public override void VisitText(string text)
        {
            base.VisitText(text);

            this.spans.Add(new Span
            {
                Type = SpanType.Text,
                Text = text
            });
        }

        public override void VisitBr(TtmlBr br)
        {
            base.VisitBr(br);

            this.spans.Add(new Span
            {
                Type = SpanType.Text,
                Text = Environment.NewLine,
            });
        }
    }
}
