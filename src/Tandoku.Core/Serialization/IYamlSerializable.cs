namespace Tandoku.Serialization;

using System.IO.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal interface IYamlSerializable<TSelf>
    where TSelf : IYamlSerializable<TSelf>
{
    static virtual async Task<TSelf> ReadYamlAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so reader is not disposed prematurely
        using var reader = file.OpenText();
        return await TSelf.ReadYamlAsync(reader);
    }

    static virtual async Task<TSelf> ReadYamlAsync(TextReader reader)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        if (reader is StringReader)
        {
            return deserializer.Deserialize<TSelf>(reader);
        }
        else
        {
            var s = await reader.ReadToEndAsync();
            return deserializer.Deserialize<TSelf>(s);
        }
    }

    virtual async Task WriteYamlAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await this.WriteYamlAsync(writer);
    }

    virtual Task WriteYamlAsync(TextWriter writer)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull |
                DefaultValuesHandling.OmitDefaults |
                DefaultValuesHandling.OmitEmptyCollections)
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
