namespace Tandoku.Tests.Content.Transforms;

using System.Collections.Immutable;
using Tandoku.Content;
using Tandoku.Content.Transforms;

public class RemoveNonJapaneseTextTransformTests
{
    [Test]
    public void EmptyText_Preserved()
    {
        Apply(new ContentBlockChunk { Text = "" })?.Text.Should().Be("");
    }

    [Test]
    public void HiraganaText_Preserved()
    {
        Apply(new ContentBlockChunk { Text = "こんにちは" })!.Text.Should().Be("こんにちは");
    }

    [Test]
    public void KanjiText_Preserved()
    {
        Apply(new ContentBlockChunk { Text = "日本語" })!.Text.Should().Be("日本語");
    }

    [Test]
    public void EnglishOnly_Removed()
    {
        Apply(new ContentBlockChunk { Text = "hello world" }).Should().BeNull();
    }

    [Test]
    public void IgnoredKanjiOnly_Removed()
    {
        // 口 入 人 日 are all in IgnoredKanji
        Apply(new ContentBlockChunk { Text = "口入人" }).Should().BeNull();
    }

    [Test]
    public void IgnoredKanaOnly_Removed()
    {
        // ロ is in IgnoredKana, single kana <= 1 -> removed (no other kana, no kanji)
        Apply(new ContentBlockChunk { Text = "ロ" }).Should().BeNull();
    }

    [Test]
    public void RoleFilter_OnlyTransformsMatchingRole()
    {
        var transform = new RemoveNonJapaneseTextTransform(ChunkRole.Secondary);
        var primary = new ContentBlockChunk { Text = "hello", Role = ChunkRole.Primary };
        // Primary role should be passed through unchanged because role doesn't match.
        ApplyWith(transform, primary).Should().NotBeNull();

        var secondary = new ContentBlockChunk { Text = "hello", Role = ChunkRole.Secondary };
        ApplyWith(transform, secondary).Should().BeNull();
    }

    private static ContentBlockChunk? Apply(ContentBlockChunk chunk) =>
        ApplyWith(new RemoveNonJapaneseTextTransform(), chunk);

    private static ContentBlockChunk? ApplyWith(ContentBlockTransform t, ContentBlockChunk chunk)
    {
        var block = new ContentBlock { Chunks = ImmutableList.Create(chunk) };
        var result = t.TransformAsync(AsAsync(block), null!).ToList().GetAwaiter().GetResult();
        return result.Single().Chunks.SingleOrDefault();
    }

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
