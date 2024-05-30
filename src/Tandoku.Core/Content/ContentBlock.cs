namespace Tandoku.Content;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Tandoku.Serialization;
using Tandoku.Yaml;

public abstract record ContentBlock : IYamlStreamSerializable<ContentBlock>
{
    private static readonly ReadOnlyMemory<byte> blocksProperty = Encoding.UTF8.GetBytes("blocks");

    public string? Id { get; init; }

#if DEBUG // TODO remove this when contract is fully specified
    private string? OriginalJson { get; set; }
#endif

    static ValueTask<ContentBlock?> IYamlStreamSerializable<ContentBlock>.DeserializeYamlDocumentAsync(
        YamlDotNet.Core.Parser yamlParser,
        YamlToJsonConverter jsonConverter)
    {
        var jsonDoc = jsonConverter.ConvertToJsonDocument(yamlParser);
        ContentBlock? block = jsonDoc.RootElement.ValueKind switch
        {
            JsonValueKind.Object => jsonDoc.RootElement.TryGetProperty(blocksProperty.Span, out _) ?
                jsonDoc.Deserialize<CompositeBlock>(jsonConverter.JsonOptions) :
                jsonDoc.Deserialize<TextBlock>(jsonConverter.JsonOptions),

            JsonValueKind.Null => null,

            _ => throw new InvalidDataException($"Unexpected document value of type '{jsonDoc.RootElement.ValueKind}' in YAML stream"),
        };
#if DEBUG
        if (block is not null)
            block.OriginalJson = jsonDoc.RootElement.ToString();
#endif
        return ValueTask.FromResult(block);
    }
}

public sealed record TextBlock : ContentBlock
{
    public string? Text { get; init; }
}

public sealed record CompositeBlock : ContentBlock
{
    public IImmutableList<TextBlock> Blocks { get; init; } = [];
}
