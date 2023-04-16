namespace Tandoku.Library;

public sealed record LibraryInfo(
    string Path,
    string DefinitionPath,
    LibraryDefinition Definition);
