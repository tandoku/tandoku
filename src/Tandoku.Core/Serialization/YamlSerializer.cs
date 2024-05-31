namespace Tandoku.Serialization;

using System.IO.Abstractions;

internal static class YamlSerializer
{
    internal static IAsyncEnumerable<T> ReadStreamAsync<T>(IFileInfo file)
        where T : IYamlStreamSerializable<T> =>
        T.ReadYamlAsync(file);

    internal static Task WriteStreamAsync<T>(IFileInfo file, IAsyncEnumerable<T> items)
        where T : IYamlStreamSerializable<T> =>
        T.WriteYamlAsync(file, items);
}
