namespace Tandoku.Content.Alignment;

public interface IContentAligner
{
    IAsyncEnumerable<ContentBlock> AlignAsync(
        IAsyncEnumerable<ContentBlock> inputBlocks,
        IAsyncEnumerable<ContentBlock> refBlocks);
}
