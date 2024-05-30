namespace Tandoku.Tests.Yaml;

using System.Text;
using System.Text.Json;
using Tandoku.Yaml;
using YamlDotNet.Core;

public class YamlToJsonConverterTests
{
    [Fact]
    public void DeserializeViaJson()
    {
        var yaml = """
            key1: value1
            key2: 42
            key3: [1,2,3]
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var obj = YamlToJsonConverter.DeserializeViaJson<TestObject>(new StringReader(yaml), options);

        obj.Should().BeEquivalentTo(
            new TestObject("value1", 42, [1, 2, 3]));
    }

    private record TestObject(string Key1, long Key2, IReadOnlyList<long> Key3);

    [Fact]
    public void ConvertYamlToJsonSimple()
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
              "key2": 42,
              "key3": [1,2,3
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

        using var jsonStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Indented = true }))
            YamlToJsonConverter.ConvertToJson(new StringReader(yaml), jsonWriter);

        Encoding.UTF8.GetString(jsonStream.ToArray())
            .Should().Be(json);
    }
}
