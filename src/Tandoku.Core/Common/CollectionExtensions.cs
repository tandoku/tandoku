namespace Tandoku;

internal static class CollectionExtensions
{
    internal static void Sort<TItem, TKey>(this List<TItem> list, Func<TItem, TKey> keySelector, IComparer<TKey>? comparer = null)
    {
        comparer ??= Comparer<TKey>.Default;
        list.Sort((x, y) => comparer.Compare(keySelector(x), keySelector(y)));
    }
}
