using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BlueMarsh.Tandoku
{
    public sealed class TextBlockSerializer
    {
        public IEnumerable<TextBlock> Deserialize(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jsonl" => DeserializeJson(path),
                ".yaml" => DeserializeYaml(path),
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
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .Build();

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

        public void Serialize(string path, IEnumerable<TextBlock> blocks)
        {
            /*
            Path.GetExtension(path).ToUpperInvariant() switch
            {
                ".JSONL" => SerializeJson(path, blocks),
                ".YAML" => SerializeYaml(path, blocks),
            };
            */

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jsonl":
                    SerializeJson(path, blocks);
                    break;

                case ".yaml":
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

            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                //.WithNamingConvention(NamingConventions.CamelCaseNamingConvention.Instance)
                .WithEventEmitter(next => new Yaml.StringQuotingEmitter(next))
                .ConfigureDefaultValuesHandling(
                    //DefaultValuesHandling.OmitEmptyCollections,
                    DefaultValuesHandling.OmitDefaults |
                    DefaultValuesHandling.OmitNull)
                .Build();
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
}
