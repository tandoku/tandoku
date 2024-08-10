namespace Tandoku.Content.Transforms;

using WanaKanaSharp;

public sealed class RemoveNonJapaneseTextTransform : ContentBlockRewriter
{
    private static readonly IReadOnlySet<char> IgnoredKana = new HashSet<char>(['ロ']);
    private static readonly IReadOnlySet<char> IgnoredKanji = new HashSet<char>(['口', '入', '人', '日']);

    public override ContentBlock? Visit(TextBlock block)
    {
        if (string.IsNullOrWhiteSpace(block.Text))
            return block;

        var kanaCount = block.Text.Count(c => WanaKana.IsKana(c) && !IgnoredKana.Contains(c));
        var allKanaCount = block.Text.Count(WanaKana.IsKana);
        if (kanaCount > 1 || (kanaCount > 0 && allKanaCount > 1))
            return block;

        var kanjiCount = block.Text.Count(c => WanaKana.IsKanji(c) && !IgnoredKanji.Contains(c));
        if (kanjiCount > 0)
            return block;

        return null;
    }
}
