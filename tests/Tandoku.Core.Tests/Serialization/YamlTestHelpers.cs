namespace Tandoku.Tests.Serialization;

using System.IO.Abstractions;
using Tandoku.Serialization;

internal static class YamlTestHelpers
{
    internal static Task<T> ReadAsync<T>(TextReader reader) where T : IYamlSerializable<T> =>
        T.ReadYamlAsync(reader);

    internal static Task<T> ReadAsync<T>(IFileInfo file) where T : IYamlSerializable<T> =>
        T.ReadYamlAsync(file);

    internal static Task WriteAsync<T>(T doc, TextWriter writer) where T : IYamlSerializable<T> =>
        doc.WriteYamlAsync(writer);

    internal static Task WriteAsync<T>(T doc, IFileInfo file) where T : IYamlSerializable<T> =>
        doc.WriteYamlAsync(file);

    internal static Task WriteStreamAsync<T>(TextWriter writer, IAsyncEnumerable<T> items)
        where T : IYamlStreamSerializable<T> =>
        T.WriteYamlAsync(writer, items);
}
