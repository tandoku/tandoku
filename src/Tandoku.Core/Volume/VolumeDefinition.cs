namespace Tandoku.Volume;

using Tandoku.Serialization;

public sealed record VolumeDefinition : IYamlSerializable<VolumeDefinition>
{
    public required string Title { get; init; }
    public string? Moniker { get; init; }
    public required string Language { get; init; }
    public string? ReferenceLanguage { get; init;  }
}
