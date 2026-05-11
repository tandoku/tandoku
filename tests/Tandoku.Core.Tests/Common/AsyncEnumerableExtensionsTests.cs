namespace Tandoku.Tests.Common;

public class AsyncEnumerableExtensionsTests
{
    [Test]
    public async Task ToList_CollectsAllItems()
    {
        var list = await Range().ToList();
        list.Should().Equal(0, 1, 2, 3);
    }

    [Test]
    public async Task ToList_EmptySource_ReturnsEmptyList()
    {
        var list = await Empty().ToList();
        list.Should().BeEmpty();
    }

    private static async IAsyncEnumerable<int> Range()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    private static async IAsyncEnumerable<int> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }
}
