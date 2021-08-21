using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace BlueMarsh.Tandoku
{
    // TODO: consider using records? May not work with YamlDotNet yet

    public sealed class TextBlock
    {
        public Image? Image { get; set; }

        public string? Text { get; set; }

        // TODO: NormalizedText (needed? try out Markdown normalization)
        //       AnnotatedText (add/remove furigana ruby to match annotation preferences)

        // TODO: rename to AlternateText, change to Dictionary<string, string>
        public string? Translation { get; set; }

        // TODO: make this nullable, only populate when used
        public List<Token> Tokens { get; } = new List<Token>();

        // TODO: replace with Source object
        public string? Location { get; set; }
    }

    public sealed class Image
    {
        public string? Name { get; set; }
        public ImageMap? Map { get; set; }
    }

    public sealed class ImageMap
    {
        public List<ImageMapLine> Lines { get; init; } = new List<ImageMapLine>();
    }

    public interface IHasBoundingBox
    {
        int[] BoundingBox { get; }

        public Rectangle ToRectangle() => Rectangle.FromLTRB(
            BoundingBox[0],
            BoundingBox[1],
            BoundingBox[6],
            BoundingBox[7]);
    }

    public sealed class ImageMapLine : IHasBoundingBox
    {
        public int[] BoundingBox { get; init; } = new int[8];
        public string? Text { get; set; }
        public List<ImageMapWord> Words { get; init; } = new List<ImageMapWord>();
    }

    public sealed class ImageMapWord : IHasBoundingBox
    {
        public int[] BoundingBox { get; init; } = new int[8];
        public string? Text { get; set; }
        public double? Confidence { get; set; }
    }

    public sealed class Token
    {
        public long? Ordinal { get; set; }
        public string? Term { get; set; }
        public int? StartOffset { get; set; }
        public int? EndOffset { get; set; }
        public int? PositionIncrement { get; set; }
        public int? PositionLength { get; set; }
        public string? BaseForm { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? InflectionForm { get; set; }
        public string? InflectionType { get; set; }
        public string? Pronunciation { get; set; }
        public string? Reading { get; set; }
    }
}
