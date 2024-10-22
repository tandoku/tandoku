namespace Tandoku.Content.Transforms;

using System.Collections.Frozen;
using WanaKanaSharp;

public sealed class RemoveNonJapaneseTextTransform(ChunkRole? role = null) : ContentBlockTransform
{
    // Note: This transform is currently intended for removing extraneous text after image OCR analysis.
    // Consider moving the 'ignored kana/kanji' to an option if this is needed for more general purpose.

    private static readonly FrozenSet<char> IgnoredKana = FrozenSet.ToFrozenSet(['ロ']);
    private static readonly FrozenSet<char> IgnoredKanji = FrozenSet.ToFrozenSet(['口', '入', '人', '日']);

    protected override ContentBlockChunk? TransformChunk(ContentBlockChunk chunk)
    {
        if (role is not null && chunk.Role != role)
            return chunk;

        if (string.IsNullOrWhiteSpace(chunk.Text))
            return chunk;

        var kanaCount = chunk.Text.Count(c => WanaKana.IsKana(c) && !IgnoredKana.Contains(c));
        var allKanaCount = chunk.Text.Count(WanaKana.IsKana);
        if (kanaCount > 1 || (kanaCount > 0 && allKanaCount > 1))
            return chunk;

        var kanjiCount = chunk.Text.Count(c => WanaKana.IsKanji(c) && !IgnoredKanji.Contains(c));
        if (kanjiCount > 0)
            return chunk;

        return null;
    }
}
