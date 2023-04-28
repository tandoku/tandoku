namespace Tandoku.Yaml;

using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

// TODO: rename to YamlToJsonConverter, ConvertToJson and DeserializeViaJson<T> methods
public static class YamlJsonWriter
{
    public static void Write(TextReader yamlReader, Utf8JsonWriter jsonWriter) =>
        Write(new Parser(yamlReader), jsonWriter);

    public static void Write(IParser yamlParser, Utf8JsonWriter jsonWriter)
    {
        var visitor = new ParsingEventVisitor(jsonWriter);
        while (yamlParser.MoveNext())
            yamlParser.Current?.Accept(visitor);
    }

    private sealed class ParsingEventVisitor : IParsingEventVisitor
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
                // TODO: infer other data types
                this.jsonWriter.WriteStringValue(e.Value);

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
    }

    private enum YamlContext
    {
        Mapping,
        Sequence,
        Property,
    }
}
