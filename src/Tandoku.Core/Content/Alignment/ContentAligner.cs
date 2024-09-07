namespace Tandoku.Content.Alignment;

using Tandoku.Common;

public interface IContentAligner
{
    IAsyncEnumerable<ContentBlock> AlignAsync(
        IAsyncEnumerable<ContentBlock> inputBlocks,
        IAsyncEnumerable<ContentBlock> refBlocks);
}

public abstract class TextBlockAligner(string refName) : IContentAligner
{
    public IAsyncEnumerable<ContentBlock> AlignAsync(
        IAsyncEnumerable<ContentBlock> inputBlocks,
        IAsyncEnumerable<ContentBlock> refBlocks)
    {
        return this.AlignAsyncCore(
            inputBlocks.Cast<ContentBlock, TextBlock>(),
            refBlocks.Cast<ContentBlock, TextBlock>());
    }

    protected abstract IAsyncEnumerable<TextBlock> AlignAsyncCore(
        IAsyncEnumerable<TextBlock> inputBlocks,
        IAsyncEnumerable<TextBlock> refBlocks);

    protected virtual TextBlock MergeBlocks(TextBlock? block, IEnumerable<TextBlock> refBlocks)
    {
        var result = block ?? new TextBlock();
        foreach (var refBlock in refBlocks)
        {
            if (result.References.TryGetValue(refName, out var reference))
            {
                reference = reference with
                {
                    Text = $"{reference.Text}{Environment.NewLine}{Environment.NewLine}{refBlock.Text}",
                    Source = MergeSource(reference.Source, refBlock.Source),
                };
                result = result with
                {
                    References = result.References.SetItem(refName, reference),
                };
            }
            else
            {
                reference = new ContentTextReference
                {
                    Text = refBlock.Text,
                    Source = refBlock.Source,
                };
                result = result with
                {
                    References = result.References.Add(refName, reference),
                };
            }
        }
        return result;
    }

    private static ContentSource? MergeSource(ContentSource? source, ContentSource? mergeSource)
    {
        if (source is not null && mergeSource is not null)
        {
            TimecodePair? mergedTimecodes = null;
            if (source.Timecodes is not null && mergeSource.Timecodes is not null)
            {
                mergedTimecodes = new TimecodePair(
                    source.Timecodes.Value.Start,
                    mergeSource.Timecodes.Value.End);
            }

            return source with
            {
                Resource = source.Resource ?? mergeSource.Resource,
                Timecodes = mergedTimecodes ?? source.Timecodes ?? mergeSource.Timecodes,
            };
        }
        return source ?? mergeSource;
    }
}
