namespace Tandoku;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class DocumentSerializerBase
{
    protected DeserializerBuilder CreateDeserializerBuilder()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance);
    }

    protected SerializerBuilder CreateSerializerBuilder()
    {
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new Yaml.FlowStyleEventEmitter(next))
            .WithEventEmitter(next => new Yaml.StringQuotingEmitter(next))
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitDefaults |
                DefaultValuesHandling.OmitEmptyCollections |
                DefaultValuesHandling.OmitNull);
    }
}

public class SingleDocumentSerializer<T> : DocumentSerializerBase
{
    public T DeserializeYaml(string path)
    {
        using var reader = File.OpenText(path);
        var deserializer = CreateDeserializerBuilder().Build();
        return deserializer.Deserialize<T>(reader);
    }

    public void SerializeYaml(string path, T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        using var writer = File.CreateText(path);
        var serializer = CreateSerializerBuilder().Build();
        serializer.Serialize(writer, item);
    }
}
