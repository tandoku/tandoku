namespace Tandoku.Tests.Common;

public class CollectionExtensionsTests
{
    [Test]
    public void Sort_ByKeySelector_DefaultComparer()
    {
        var list = new List<string> { "ccc", "a", "bb" };
        list.Sort(s => s.Length);
        list.Should().Equal("a", "bb", "ccc");
    }

    [Test]
    public void Sort_ByKeySelector_CustomComparer()
    {
        var list = new List<string> { "ccc", "a", "bb" };
        list.Sort(s => s.Length, Comparer<int>.Create((x, y) => y.CompareTo(x)));
        list.Should().Equal("ccc", "bb", "a");
    }

    [Test]
    public void Sort_StableForEqualKeys_KeepsRelativeOrderOrSwaps()
    {
        // List<T>.Sort is not guaranteed stable, but for equal keys all we can assert is
        // that all original elements remain present.
        var list = new List<string> { "x", "y", "z" };
        list.Sort(_ => 0);
        list.Should().BeEquivalentTo(new[] { "x", "y", "z" });
    }
}
