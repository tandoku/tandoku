namespace Tandoku.Library;

public sealed record LibraryInfo(
    string Path,
    LibraryVersion Version,
    string DefinitionPath,
    LibraryDefinition Definition);
