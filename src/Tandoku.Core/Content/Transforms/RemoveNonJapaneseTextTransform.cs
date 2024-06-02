namespace Tandoku.Content.Transforms;

using WanaKanaSharp;

public sealed class RemoveNonJapaneseTextTransform : ContentBlockRewriter
{
    public override ContentBlock? Visit(TextBlock block)
    {
        return (string.IsNullOrWhiteSpace(block.Text) ||
            block.Text.Any(c => WanaKana.IsKana(c) || WanaKana.IsKanji(c))) ? block : null;
    }
}
