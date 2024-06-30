namespace Tandoku.Serialization;

using System.IO.Abstractions;
using Tandoku.Content;
using Tandoku.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

internal interface IYamlStreamSerializable<TSelf>
    where TSelf : IYamlStreamSerializable<TSelf>
{
    static virtual async IAsyncEnumerable<TSelf> ReadYamlAsync(IFileInfo file)
    {
        // TODO - consider moving the exception handling into ReadYamlAsync(TextReader...)
        // and include context from the TextReader (ideally via base class but maybe StreamReader)
        // Then we can keep this simple with the await foreach pattern

        // Note: this method must be 'async' so reader is not disposed prematurely
        using var reader = file.OpenText();
        await using var enumerator = TSelf.ReadYamlAsync(reader).GetAsyncEnumerator();
        while (true)
        {
            try
            {
                if (!(await enumerator.MoveNextAsync()))
                    break;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"{ex.Message}{Environment.NewLine}File: {file}", ex);
            }

            yield return enumerator.Current;
        }
    }

    static virtual async IAsyncEnumerable<TSelf> ReadYamlAsync(TextReader reader)
    {
        // Note: YamlDotNet Parser skips comments unless Scanner is explicitly created to include comments
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();

        var jsonConverter = new YamlToJsonConverter(SerializationFactory.JsonOptions);

        while (parser.Accept<DocumentStart>(out _))
        {
            var item = await TSelf.DeserializeYamlDocumentAsync(parser, jsonConverter);
            if (item is not null)
                yield return item;
        }

        parser.Consume<StreamEnd>();
    }

    // NOTE: this method is currently synchronous as YamlDotNet does not have an async API
    // Another possibility is to manually buffer one document at a time from TextReader by looking for line consisting of document end mark (---)
    static virtual ValueTask<TSelf?> DeserializeYamlDocumentAsync(Parser yamlParser, YamlToJsonConverter jsonConverter) =>
        ValueTask.FromResult(jsonConverter.DeserializeViaJson<TSelf?>(yamlParser));

    static virtual async Task WriteYamlAsync(IFileInfo file, IAsyncEnumerable<TSelf> items)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await TSelf.WriteYamlAsync(writer, items);
    }

    static virtual async Task WriteYamlAsync(TextWriter writer, IAsyncEnumerable<TSelf> items)
    {
        var serializer = SerializationFactory.CreateYamlSerializer();
        bool first = true;
        await foreach (var item in items)
        {
            if (first)
                first = false;
            else
                await writer.WriteLineAsync("---");

            var document = serializer.Serialize(item);
            await writer.WriteAsync(document);
        }
    }
}
