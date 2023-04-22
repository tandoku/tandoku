namespace Tandoku.Volume;

public sealed record VolumeInfo(
    string Path,
    VolumeVersion Version,
    string DefinitionPath,
    VolumeDefinition Definition);
