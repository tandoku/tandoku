namespace Tandoku.Volume;

public sealed record VolumeInfo(
    string Path,
    string Slug,
    VolumeVersion Version,
    string DefinitionPath,
    VolumeDefinition Definition);

public sealed record RenameResult(string OriginalPath, string RenamedPath);
