namespace Tandoku.Volume;

using System.Collections.Immutable;
using Tandoku.Serialization;

public sealed record VolumeDefinition : IYamlSerializable<VolumeDefinition>
{
    public string? Title { get; init; }
    // TODO - remove
    public string? Moniker { get; init; }
    public required string Language { get; init; }
    //public string? ReferenceLanguage { get; init; }
    public IImmutableSet<string> Tags { get; init; } = ImmutableSortedSet<string>.Empty; // TODO: restrictions on tag values?
    public IImmutableDictionary<string, LinkedVolume> LinkedVolumes { get; init; } = ImmutableSortedDictionary<string, LinkedVolume>.Empty;
    public string? Workflow { get; init; }
}

public sealed record LinkedVolume
{
    public string? Path { get; init; }
    public string? Moniker { get; init; }
}
