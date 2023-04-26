namespace Tandoku.Tests.Yaml;

using System.Text;
using System.Text.Json;
using Tandoku.Yaml;
using YamlDotNet.Core;

public class YamlJsonWriterTests
{
    [Fact]
    public void WriteYamlToJsonSimple()
    {
        var yaml = """
            key1: value1
            key2: 42
            key3: [1,2,3]
            key4: [hello,world]
            key5: {nested: object}
            """;

        var json = """
            {
              "key1": "value1",
              "key2": "42",
              "key3": [
                "1",
                "2",
                "3"
              ],
              "key4": [
                "hello",
                "world"
              ],
              "key5": {
                "nested": "object"
              }
            }
            """;

        var yamlParser = new Parser(new StringReader(yaml));
        using var jsonStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Indented = true }))
            YamlJsonWriter.Write(yamlParser, jsonWriter);

        Encoding.UTF8.GetString(jsonStream.ToArray())
            .Should().Be(json);
    }
}
