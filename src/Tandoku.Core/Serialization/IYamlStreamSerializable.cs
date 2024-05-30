namespace Tandoku.Serialization;

using System.IO.Abstractions;
using Tandoku.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

internal interface IYamlStreamSerializable<TSelf>
    where TSelf : IYamlStreamSerializable<TSelf>
{
    static virtual async IAsyncEnumerable<TSelf> ReadYamlAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so reader is not disposed prematurely
        using var reader = file.OpenText();
        await foreach (var item in TSelf.ReadYamlAsync(reader))
            yield return item;
    }

    // NOTE: this method is currently synchronous as YamlDotNet does not have an async API
    static virtual async IAsyncEnumerable<TSelf> ReadYamlAsync(TextReader reader)
    {
        // Note: YamlDotNet Parser skips comments unless Scanner is explicitly created to include comments
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();

        var jsonConverter = new YamlToJsonConverter(SerializationFactory.JsonOptions);

        while (parser.Accept<DocumentStart>(out _))
        {
            var block = jsonConverter.DeserializeViaJson<TSelf>(parser);
            if (block is not null)
                yield return block;
        }

        parser.Consume<StreamEnd>();
    }
}
