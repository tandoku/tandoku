﻿namespace Tandoku;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// TODO: further refactor this class into a DocumentStreamSerializer<T>
public sealed class TextBlockSerializer : DocumentSerializerBase
{
    public IEnumerable<TextBlock> Deserialize(string path, TextBlockFormat? format = null)
    {
        return (format ?? TextBlockFormatExtensions.FromFilePath(path)) switch
        {
            TextBlockFormat.Jsonl => DeserializeJson(path),
            TextBlockFormat.Yaml => DeserializeYaml(path),
            _ => throw new ArgumentException("Unexpected extension for 'path'."),
        };
    }

    public IEnumerable<TextBlock> DeserializeJson(string path)
    {
        using var reader = File.OpenText(path);
        string? line;
        while ((line = reader.ReadLine()) != null)
            yield return JsonSerializer.Deserialize<TextBlock>(line);
    }

    public IEnumerable<TextBlock> DeserializeYaml(string path)
    {
        var deserializer = CreateDeserializerBuilder().Build();

        using var reader = File.OpenText(path);
        var parser = new YamlDotNet.Core.Parser(reader);
        parser.Consume<StreamStart>();

        while (parser.Current is DocumentStart)
        {
            var block = deserializer.Deserialize<TextBlock>(parser);
            if (block is not null)
                yield return block;

            // TODO: handle comments?? or Deserialize does this already?
            // (can comments appear *before* DocumentStart?)
        }

        parser.Consume<StreamEnd>();
    }

    public void Serialize(string path, IEnumerable<TextBlock> blocks, TextBlockFormat? format = null)
    {
        switch (format ?? TextBlockFormatExtensions.FromFilePath(path))
        {
            case TextBlockFormat.Jsonl:
                SerializeJson(path, blocks);
                break;

            case TextBlockFormat.Yaml:
                SerializeYaml(path, blocks);
                break;

            default:
                throw new ArgumentException("Unexpected extension for 'path'.");
        }
    }

    public void SerializeJson(string path, IEnumerable<TextBlock> blocks)
    {
        /*
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        foreach (var block in blocks)
        {
            JsonSerializer.Serialize(writer, block);
            writer.Flush();
            stream.WriteByte((byte)'\n');
        }*/

        // TODO: use camelCase for consistency with YAML
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        using var writer = File.CreateText(path);
        foreach (var block in blocks)
            writer.WriteLine(JsonSerializer.Serialize(block, options));
    }

    public void SerializeYaml(string path, IEnumerable<TextBlock> blocks)
    {
        using var writer = File.CreateText(path);

        // TODO: consider implementing IEmitter wrapper that ignores Stream/Document events
        // (pass this to Serialize method and emit these events ourselves here)
        //var emitter = new YamlDotNet.Core.Emitter(writer);
        //emitter.Emit(new YamlDotNet.Core.Events.StreamStart());

        var serializer = CreateSerializerBuilder().Build();
        bool first = true;
        foreach (var block in blocks)
        {
            if (first)
                first = false;
            else
                writer.WriteLine();

            //emitter.Emit(new YamlDotNet.Core.Events.DocumentStart());
            //serializer.Serialize(emitter, block);
            //emitter.Emit(new YamlDotNet.Core.Events.DocumentEnd(isImplicit: false));

            serializer.Serialize(writer, block);

            writer.Write("---");
        }

        //emitter.Emit(new YamlDotNet.Core.Events.StreamEnd());
    }
}
