namespace Tandoku.Library;

using Tandoku.Serialization;

public sealed record LibraryDefinition : IYamlSerializable<LibraryDefinition>
{
    public required string Language { get; init; }
    public string? ReferenceLanguage { get; init;  }
}
