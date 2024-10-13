namespace Tandoku.Content;

using System.Collections.Immutable;
using System.Text.Json;
using Tandoku.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

public record Block
{
    [YamlMember(Order = 20)]
    public BlockImage? Image { get; init; }

    [YamlMember(Order = 40)]
    public BlockSource? Source { get; init; }

    protected Block CloneBlock() => new(this);
} 

public sealed record ContentBlock : Block, IYamlStreamSerializable<ContentBlock>
{
    [YamlMember(Order = 10)]
    public string? Id { get; init; }

    [YamlMember(Order = 30)]
    public ContentBlockAudio? Audio { get; init; }

    [YamlMember(Order = 50)]
    public IImmutableDictionary<string, Block> References { get; init; } =
        ImmutableSortedDictionary<string, Block>.Empty;

    [YamlMember(Order = 60)]
    public IImmutableList<ContentBlockChunk> Chunks { get; init; } = [];

    public ContentBlockChunk SingleChunk() => this.Chunks.Count switch
    {
        0 => ContentBlockChunk.Empty,
        1 => this.Chunks[0],
        _ => throw new InvalidOperationException("Multiple chunks not expected on this block."),
    };

    // It seems like ```new Block(this)``` should just work since the copy constructor
    // is accessible via base() from a constructor on this class, but it doesn't work.
    public Block ToBlock() => this.CloneBlock();

    public string ToJsonString() =>
        JsonSerializer.Serialize(this, SerializationFactory.JsonOptions);

    public static ContentBlock? DeserializeJson(string json) =>
        JsonSerializer.Deserialize<ContentBlock>(json, SerializationFactory.JsonOptions);
}

public interface IMediaReference
{
    string? Name { get; }
}

public sealed record BlockImage : IMediaReference
{
    public required string Name { get; init; }
}

public sealed record ContentBlockAudio : IMediaReference
{
    public required string Name { get; init; }
}

public sealed record BlockSource
{
    public int? Ordinal { get; init; }
    public TimecodePair? Timecodes { get; init; }
    public string? Note { get; init; }
    public string? Resource { get; init; }
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
        // (would need special handling for negative values)
        return $"{this.Start:c} --> {this.End:c}";
    }
}

public record Chunk : IMarkdownText
{
    [YamlMember(ScalarStyle = ScalarStyle.Literal, Order = 10)]
    public string? Text { get; init; }

    // TODO Actor, Kind

    [YamlMember(Order = 40)]
    public ChunkImage? Image { get; init; }
}

public sealed record ContentBlockChunk : Chunk
{
    public static ContentBlockChunk Empty { get; } = new();

    public ContentBlockChunk() { }

    public ContentBlockChunk(Chunk chunk) : base(chunk) { }

    // TODO Tokens

    [YamlMember(Order = 90)]
    public IImmutableDictionary<string, Chunk> References { get; init; } =
        ImmutableSortedDictionary<string, Chunk>.Empty;
}

public sealed record ChunkImage
{
    // TODO Bounds
    public IImmutableList<ImageTextSpan> TextSpans { get; init; } = [];
}

public sealed record ImageTextSpan
{
    [YamlMember(Order = 10)]
    public required string Text { get; init; }

    // TODO Bounds

    [YamlMember(Order = 30)]
    public double Confidence { get; init; }
}
