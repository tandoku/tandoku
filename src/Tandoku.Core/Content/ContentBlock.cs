namespace Tandoku.Content;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tandoku.Serialization;
using Tandoku.Yaml;

[JsonDerivedType(typeof(TextBlock))]
[JsonDerivedType(typeof(CompositeBlock))]
public abstract record ContentBlock : IYamlStreamSerializable<ContentBlock>
{
    private static readonly ReadOnlyMemory<byte> blocksProperty = Encoding.UTF8.GetBytes("blocks");

    public string? Id { get; init; }

    public ContentImage? Image { get; init; }

    public ContentSource? Source { get; init; }

#if DEBUG // TODO remove this when contract is fully specified
    private string? OriginalJson { get; set; }
#endif

    public string ToJsonString() =>
        JsonSerializer.Serialize(this, SerializationFactory.JsonOptions);

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

            _ => throw new InvalidDataException($"Unexpected document value of type '{jsonDocument.RootElement.ValueKind}' in YAML stream"),
        };
    }
}

public sealed record ContentImage
{
    public string? Name { get; init; }
}

public sealed record ContentSource
{
    public string? Resource { get; init; }
}

public sealed record TextBlock : ContentBlock
{
    public string? Text { get; init; }
    public IImmutableDictionary<string, ContentReference> References { get; init; } = ImmutableSortedDictionary<string, ContentReference>.Empty;
}

public sealed record ContentReference
{
    public string? Text { get; init; }
}

public sealed record CompositeBlock : ContentBlock
{
    public IImmutableList<TextBlock> Blocks { get; init; } = [];
}
