namespace Tandoku.Serialization;

using System.IO.Abstractions;

internal static class YamlSerializer
{
    internal static IAsyncEnumerable<T> ReadStreamAsync<T>(IFileInfo file)
        where T : IYamlStreamSerializable<T> =>
        T.ReadYamlAsync(file);
}
