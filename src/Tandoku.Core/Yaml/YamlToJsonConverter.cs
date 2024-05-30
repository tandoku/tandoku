namespace Tandoku.Yaml;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

public sealed partial class YamlToJsonConverter
{
    private ArrayBufferWriter<byte>? jsonBufferWriter;

    public YamlToJsonConverter(JsonSerializerOptions? options = null)
    {
        this.JsonOptions = options;
    }

    public JsonSerializerOptions? JsonOptions { get; init; }

    public T DeserializeViaJson<T>(TextReader yamlReader) =>
        this.DeserializeViaJson<T>(new Parser(yamlReader));

    public T DeserializeViaJson<T>(IParser yamlParser)
    {
        this.jsonBufferWriter ??= CreateJsonBufferWriter();
        return DeserializeViaJson<T>(yamlParser, this.JsonOptions, this.jsonBufferWriter);
    }

    public JsonDocument ConvertToJsonDocument(TextReader yamlReader) =>
        this.ConvertToJsonDocument(new Parser(yamlReader));

    public JsonDocument ConvertToJsonDocument(IParser yamlParser)
    {
        this.jsonBufferWriter ??= CreateJsonBufferWriter();
        return ConvertToJsonDocument(yamlParser, this.jsonBufferWriter);
    }

    public static T DeserializeViaJson<T>(
        TextReader yamlReader,
        JsonSerializerOptions? options = null,
        ArrayBufferWriter<byte>? bufferWriter = null) =>
        DeserializeViaJson<T>(new Parser(yamlReader), options, bufferWriter);

    public static T DeserializeViaJson<T>(
        IParser yamlParser,
        JsonSerializerOptions? options = null,
        ArrayBufferWriter<byte>? bufferWriter = null)
    {
        WriteToJsonWithBuffer(yamlParser, ref bufferWriter);
        return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options) ??
            throw new InvalidDataException();
    }

    public static JsonDocument ConvertToJsonDocument(
        TextReader yamlReader,
        ArrayBufferWriter<byte>? bufferWriter = null) =>
        ConvertToJsonDocument(new Parser(yamlReader), bufferWriter);

    public static JsonDocument ConvertToJsonDocument(
        IParser yamlParser,
        ArrayBufferWriter<byte>? bufferWriter = null)
    {
        WriteToJsonWithBuffer(yamlParser, ref bufferWriter);
        return JsonDocument.Parse(bufferWriter.WrittenMemory);
    }

    public static void WriteToJson(TextReader yamlReader, Utf8JsonWriter jsonWriter) =>
        WriteToJson(new Parser(yamlReader), jsonWriter);

    public static void WriteToJson(IParser yamlParser, Utf8JsonWriter jsonWriter)
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

    private static ArrayBufferWriter<byte> CreateJsonBufferWriter() =>
        new(JsonSerializerOptions.Default.DefaultBufferSize);

    private static void WriteToJsonWithBuffer(
        IParser yamlParser,
        [NotNull] ref ArrayBufferWriter<byte>? bufferWriter)
    {
        if (bufferWriter is null)
            bufferWriter = CreateJsonBufferWriter();
        else
            bufferWriter.ResetWrittenCount();

        using (var jsonWriter = new Utf8JsonWriter(bufferWriter))
            WriteToJson(yamlParser, jsonWriter);
    }

    private sealed partial class ParsingEventVisitor : IParsingEventVisitor
    {
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
                if (e.IsPlainImplicit && GetPlainJsonValueRegex().IsMatch(e.Value))
                {
                    this.jsonWriter.WriteRawValue(e.Value);
                }
                else if (e.IsPlainImplicit && string.IsNullOrEmpty(e.Value))
                {
                    this.jsonWriter.WriteNullValue();
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
