namespace Tandoku.Common;

internal static class AsyncEnumerableExtensions
{
    internal static async IAsyncEnumerable<TCast> Cast<TItem, TCast>(this IAsyncEnumerable<TItem> items)
        where TCast : TItem
    {
        await foreach (var item in items)
            yield return (TCast)item!;
    }

    internal static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> items)
    {
        var list = new List<T>();
        await foreach (var item in items)
            list.Add(item);
        return list;
    }
}
