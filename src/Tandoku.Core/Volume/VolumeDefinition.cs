namespace Tandoku.Volume;

using System.Collections.Immutable;
using Tandoku.Serialization;

public sealed record VolumeDefinition : IYamlSerializable<VolumeDefinition>
{
    public required string Title { get; init; }
    public string? Moniker { get; init; }
    public required string Language { get; init; }
    //public string? ReferenceLanguage { get; init;  }
    public IImmutableSet<string> Tags { get; init; } = []; // TODO: restrictions on tag values?
    public IImmutableDictionary<string, LinkedVolume> LinkedVolumes { get; init; } = ImmutableSortedDictionary<string, LinkedVolume>.Empty;
}

public sealed record LinkedVolume
{
    public string? Path { get; init; }
    public string? Moniker { get; init; }
}
