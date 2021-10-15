namespace BlueMarsh.Tandoku;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

// TODO: consider using records? May not work with YamlDotNet yet

public sealed class TextBlock
{
    public Image? Image { get; set; }

    [YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? Text { get; set; }

    // TODO: NormalizedText (needed? try out Markdown normalization)
    //       AnnotatedText (add/remove furigana ruby to match annotation preferences)

    // TODO: rename to AlternateText, change to Dictionary<string, string>
    // OR Alternates with Image and Text under
    public string? Translation { get; set; }

    // TODO: make this nullable, only populate when used? (YamlDotNet can omit empty collections
    // but not sure if possible with System.Text.Json)
    public List<Token> Tokens { get; init; } = new List<Token>();

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
