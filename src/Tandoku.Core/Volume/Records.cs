namespace Tandoku.Volume;

public sealed record VolumeInfo(
    string Path,
    VolumeVersion Version,
    string DefinitionPath,
    VolumeDefinition Definition);

public sealed record RenameResult(string OriginalPath, string RenamedPath);
