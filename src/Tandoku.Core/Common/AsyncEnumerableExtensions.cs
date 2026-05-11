namespace Tandoku;

internal static class AsyncEnumerableExtensions
{
    internal static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> items)
    {
        var list = new List<T>();
        await foreach (var item in items)
            list.Add(item);
        return list;
    }
}
