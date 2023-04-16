namespace Tandoku.Library;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed record LibraryDefinition
{
    public required string Language { get; init; }
    public string? ReferenceLanguage { get; init;  }

    // TODO: refactor this into a separate interface/mixin
    public Task WriteYamlAsync(TextWriter writer)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        if (writer is StringWriter)
        {
            serializer.Serialize(writer, this);
            return Task.CompletedTask;
        }
        else
        {
            var stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, this);
            return writer.WriteAsync(stringWriter.GetStringBuilder());
        }
    }
}
