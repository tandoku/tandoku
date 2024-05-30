namespace Tandoku.Yaml;

using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

// TODO: rename to YamlToJsonConverter, ConvertToJson and DeserializeViaJson<T> methods
public static partial class YamlToJsonConverter
{
    public static T DeserializeViaJson<T>(TextReader yamlReader, JsonSerializerOptions? options = null) =>
        DeserializeViaJson<T>(new Parser(yamlReader), options);

    public static T DeserializeViaJson<T>(IParser yamlParser, JsonSerializerOptions? options = null)
    {
        var bufferWriter = new ArrayBufferWriter<byte>(
            JsonSerializerOptions.Default.DefaultBufferSize);
        using (var jsonWriter = new Utf8JsonWriter(bufferWriter))
            ConvertToJson(yamlParser, jsonWriter);

        return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options) ??
            throw new InvalidDataException();
    }
    public static void ConvertToJson(TextReader yamlReader, Utf8JsonWriter jsonWriter) =>
        ConvertToJson(new Parser(yamlReader), jsonWriter);

    public static void ConvertToJson(IParser yamlParser, Utf8JsonWriter jsonWriter)
    {
        bool singleDocument = yamlParser.Accept<DocumentStart>(out _);
        var visitor = new ParsingEventVisitor(jsonWriter);
        while (yamlParser.MoveNext())
        {
            if (singleDocument && yamlParser.TryConsume<DocumentEnd>(out _))
                break;

            yamlParser.Current?.Accept(visitor);
        }
    }

    private sealed partial class ParsingEventVisitor : IParsingEventVisitor
    {
        private static Regex plainJsonValueRegex = GetPlainJsonValueRegex();

        private readonly Utf8JsonWriter jsonWriter;
        private readonly Stack<YamlContext> contextStack = new();

        internal ParsingEventVisitor(Utf8JsonWriter jsonWriter)
        {
            this.jsonWriter = jsonWriter;
        }

        public void Visit(AnchorAlias e)
        {
            throw new NotSupportedException();
        }

        public void Visit(StreamStart e)
        {
        }

        public void Visit(StreamEnd e)
        {
        }

        public void Visit(DocumentStart e)
        {
        }

        public void Visit(DocumentEnd e)
        {
        }

        public void Visit(Scalar e)
        {
            var context = this.contextStack.Count > 0 ? this.contextStack.Peek() : (YamlContext?)null;
            if (context == YamlContext.Mapping)
            {
                this.jsonWriter.WritePropertyName(e.Value);
                this.contextStack.Push(YamlContext.Property);
            }
            else
            {
                if (e.IsPlainImplicit && plainJsonValueRegex.IsMatch(e.Value))
                {
                    this.jsonWriter.WriteRawValue(e.Value);
                }
                else
                {
                    this.jsonWriter.WriteStringValue(e.Value);
                }

                if (context == YamlContext.Property)
                    this.contextStack.Pop();
            }
        }

        public void Visit(SequenceStart e)
        {
            if (this.contextStack.TryPeek(out var context) && context == YamlContext.Property)
                this.contextStack.Pop();

            this.jsonWriter.WriteStartArray();
            this.contextStack.Push(YamlContext.Sequence);
        }

        public void Visit(SequenceEnd e)
        {
            this.jsonWriter.WriteEndArray();
            this.contextStack.Pop();
        }

        public void Visit(MappingStart e)
        {
            if (this.contextStack.TryPeek(out var context) && context == YamlContext.Property)
                this.contextStack.Pop();

            this.jsonWriter.WriteStartObject();
            this.contextStack.Push(YamlContext.Mapping);
        }

        public void Visit(MappingEnd e)
        {
            this.jsonWriter.WriteEndObject();
            this.contextStack.Pop();
        }

        public void Visit(Comment e)
        {
        }

        // Patterns from https://yaml.org/spec/1.2/spec.html#id2804356
        [GeneratedRegex(@"^(null|true|false|-?([0-9]+)(\.[0-9]*)?([eE][-+]?[0-9]+)?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex GetPlainJsonValueRegex();
    }

    private enum YamlContext
    {
        Mapping,
        Sequence,
        Property,
    }
}
