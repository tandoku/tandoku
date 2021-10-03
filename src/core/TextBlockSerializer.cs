using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlueMarsh.Tandoku
{
    public sealed class TextBlockSerializer
    {
        public IEnumerable<TextBlock> Deserialize(string path)
        {
            using var reader = File.OpenText(path);
            string? line;
            while ((line = reader.ReadLine()) != null)
                yield return JsonSerializer.Deserialize<TextBlock>(line);
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

            switch (Path.GetExtension(path).ToUpperInvariant())
            {
                case ".JSONL":
                    SerializeJson(path, blocks);
                    break;

                case ".YAML":
                    SerializeYaml(path, blocks);
                    break;
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
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
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
