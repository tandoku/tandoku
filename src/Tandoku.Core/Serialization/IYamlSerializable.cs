namespace Tandoku.Serialization;

using System.IO.Abstractions;
using Tandoku.Yaml;

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
#if YAML_DESERIALIZER
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithAttemptingUnquotedStringTypeDeserialization()
            .WithNodeDeserializer(new ImmutableSetNodeDeserializer<string>()) // TODO: figure out how to use factory to create these dynamically (or do this within node deserializer)
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
#elif YAML_DESERIALIZER_JSON_DESERIALIZER
        reader = reader as StringReader ??
            new StringReader(await reader.ReadToEndAsync());

        var deserializer = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
        var o = deserializer.Deserialize(reader);

        var jsonStream = new MemoryStream();
        using (var jsonWriter = new StreamWriter(jsonStream, leaveOpen: true))
        {
            var serializer = new SerializerBuilder()
                .WithQuotingNecessaryStrings()
                .JsonCompatible()
                .Build();
            serializer.Serialize(jsonWriter, o);
        }
        jsonStream.Position = 0;

        var options = SerializationFactory.JsonOptions;
        return JsonSerializer.Deserialize<TSelf>(jsonStream, options) ??
            throw new InvalidDataException();
#else
        reader = reader as StringReader ??
            new StringReader(await reader.ReadToEndAsync());

        var options = SerializationFactory.JsonOptions;
        return YamlToJsonConverter.DeserializeViaJson<TSelf>(reader, options);
#endif
    }

    virtual async Task WriteYamlAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await this.WriteYamlAsync(writer);
    }

    virtual Task WriteYamlAsync(TextWriter writer)
    {
        var serializer = SerializationFactory.CreateYamlSerializer();

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
