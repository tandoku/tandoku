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
    }
}
