namespace Tandoku;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

// TODO: consider using records? Won't work with YamlDotNet yet
// potential workaround: deserialize YAML with YamlDotNet, write as JsonCompatible and deserialize to objects with System.Text.Json
// (continue to serialize to YAML using YamlDotNet) - but would need YamlDotNet to deserialize non-string primitive types appropriately
// (SharpYaml may be another option as I think it has support for this?)

public sealed class TextBlock
{
    // TODO: move Image below Text, ContentKind
    public Image? Image { get; set; }

    [YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? Text { get; set; }

    public ContentKind ContentKind { get; set; }

    public string? Actor { get; set; }

    // TODO: NormalizedText (needed? try out Markdown normalization)
    //       AnnotatedText (add/remove furigana ruby to match annotation preferences)

    // TODO: rename to AlternateText, change to Dictionary<string, string>
    // OR Alternates with Image and Text under (OR Reference?)
    public string? Translation { get; set; }

    // TODO: make this nullable, only populate when used? (YamlDotNet can omit empty collections
    // but not sure if possible with System.Text.Json)
    public List<Token> Tokens { get; init; } = new List<Token>();

    // TODO: remove this, replaced by Source
    public string? Location { get; set; }

    public BlockSource? Source { get; set; }

    public TextBlock Clone()
    {
        // TODO: copy additional properties... or switch to records
        return new TextBlock
        {
            Text = Text,
            ContentKind = ContentKind,
            Actor = Actor,
            Source = Source?.Clone(),
        };
    }
}

public enum ContentKind
{
    Primary,
    Secondary,
    Meta,
    SoundEffect,
    OnScreenText,
    Lyrics,
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

public sealed class BlockSource
{
    public TimecodePair? Timecodes { get; set; }

    public BlockSource Clone()
    {
        return new BlockSource
        {
            Timecodes = Timecodes,
        };
    }
}

public struct TimecodePair
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }

    [YamlIgnore]
    public TimeSpan Duration => End - Start;
}
