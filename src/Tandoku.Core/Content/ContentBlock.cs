namespace Tandoku.Content;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Markdig;
using Tandoku.Serialization;
using Tandoku.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

[JsonDerivedType(typeof(TextBlock))]
[JsonDerivedType(typeof(CompositeBlock))]
public abstract record ContentBlock : IYamlStreamSerializable<ContentBlock>
{
    private static readonly ReadOnlyMemory<byte> blocksProperty = Encoding.UTF8.GetBytes("blocks");

    public string? Id { get; init; }

    public ContentImage? Image { get; init; }

    public ContentAudio? Audio { get; init; }

    public ContentSource? Source { get; init; }

#if DEBUG // TODO remove this when contract is fully specified
    private string? OriginalJson { get; set; }
#endif

    public string ToJsonString() =>
        JsonSerializer.Serialize(this, SerializationFactory.JsonOptions);

    internal abstract T Accept<T>(ContentBlockVisitor<T> visitor);

    static ValueTask<ContentBlock?> IYamlStreamSerializable<ContentBlock>.DeserializeYamlDocumentAsync(
        YamlDotNet.Core.Parser yamlParser,
        YamlToJsonConverter jsonConverter)
    {
        var jsonDoc = jsonConverter.ConvertToJsonDocument(yamlParser);
        var block = Deserialize(jsonDoc);
#if DEBUG
        if (block is not null)
            block.OriginalJson = jsonDoc.RootElement.ToString();
#endif
        return ValueTask.FromResult(block);
    }

    internal static ContentBlock? Deserialize(JsonDocument jsonDocument)
    {
        return jsonDocument.RootElement.ValueKind switch
        {
            JsonValueKind.Object => jsonDocument.RootElement.TryGetProperty(blocksProperty.Span, out _) ?
                jsonDocument.Deserialize<CompositeBlock>(SerializationFactory.JsonOptions) :
                jsonDocument.Deserialize<TextBlock>(SerializationFactory.JsonOptions),

            JsonValueKind.Null => null,

            _ => throw new InvalidDataException(
                    $"Unexpected document value of type '{jsonDocument.RootElement.ValueKind}' in YAML stream"),
        };
    }
}

public sealed record ContentImage
{
    public string? Name { get; init; }
    public ContentImageRegion? Region { get; init; }
}

public sealed record ContentImageRegion
{
    // TODO BoundingBox
    public IImmutableList<ContentRegionSegment> Segments { get; init; } = [];
}

public sealed record ContentRegionSegment
{
    public required string Text { get; init; }
    public double Confidence { get; init; }
}

public sealed record ContentAudio
{
    public string? Name { get; init; }
}

public sealed record ContentSource
{
    public int? Ordinal { get; init; }
    public string? Resource { get; init; }
    public TimecodePair? Timecodes { get; init; }
}

public readonly record struct TimecodePair(TimeSpan Start, TimeSpan End) : IComparable<TimecodePair>
{
    [YamlIgnore]
    public readonly TimeSpan Duration => this.End - this.Start;

    public int CompareTo(TimecodePair other)
    {
        var result = this.Start.CompareTo(other.Start);
        return result != 0 ? result : this.End.CompareTo(other.End);
    }

    public override string ToString()
    {
        // TODO: consider using custom format string to use only 3 fractional digits instead of default 7
        return $"{this.Start:c} --> {this.End:c}";
    }
}

public sealed record TextBlock : ContentBlock, IMarkdownText
{
    [YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? Text { get; init; }
    public IImmutableDictionary<string, ContentTextReference> References { get; init; } =
        ImmutableSortedDictionary<string, ContentTextReference>.Empty;

    internal override T Accept<T>(ContentBlockVisitor<T> visitor) => visitor.Visit(this);
}

public record ContentReference
{
    public ContentImage? Image { get; init; }
    public ContentSource? Source { get; init; }
}

public sealed record ContentTextReference : ContentReference, IMarkdownText
{
    [YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? Text { get; init; }
}

public sealed record CompositeBlock : ContentBlock
{
    public IImmutableList<TextBlock> Blocks { get; init; } = [];
    public IImmutableDictionary<string, ContentReference> References { get; init; } =
        ImmutableSortedDictionary<string, ContentReference>.Empty;

    internal override T Accept<T>(ContentBlockVisitor<T> visitor) => visitor.Visit(this);
}
