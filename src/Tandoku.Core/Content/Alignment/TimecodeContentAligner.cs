namespace Tandoku.Content.Alignment;

using System.Collections.Generic;
using Tandoku.Common;

public sealed class TimecodeContentAligner(string refName) :
    TextBlockAligner(refName)
{
    private const double OverlapThreshold = 0.3;

    protected override async IAsyncEnumerable<TextBlock> AlignAsyncCore(
        IAsyncEnumerable<TextBlock> inputBlocks,
        IAsyncEnumerable<TextBlock> refBlocks)
    {
        var refList = await refBlocks.ToList<TextBlock?>();
        var aligned = new List<(TextBlock? Block, List<TextBlock> RefBlocks)>();

        await foreach (var block in inputBlocks)
        {
            var overlapIndex = GetMostOverlappingBlock(block, refList, b => b);
            if (overlapIndex >= 0)
            {
                aligned.Add((block, [refList[overlapIndex]!]));
                refList[overlapIndex] = null;
            }
            else
            {
                aligned.Add((block, []));
            }
        }

        foreach (var refBlock in refList)
        {
            if (refBlock is null)
                continue;

            var overlapIndex = GetMostOverlappingBlock(refBlock, aligned, n => n.Block);
            if (overlapIndex >= 0)
            {
                aligned[overlapIndex].RefBlocks.Add(refBlock);
            }
            else
            {
                aligned.Add((null, [refBlock]));
            }
        }

        var alignedSorted = aligned.OrderBy(
            n => n.Block?.Source?.Timecodes?.Start ?? n.RefBlocks.FirstOrDefault()?.Source?.Timecodes?.Start);

        foreach (var result in alignedSorted)
        {
            yield return this.MergeBlocks(
                result.Block,
                result.RefBlocks.OrderBy(b => b.Source?.Timecodes?.Start));
        }
    }

    private static int GetMostOverlappingBlock<T>(
        TextBlock block,
        IReadOnlyList<T> searchList,
        Func<T, TextBlock?> getSearchBlock)
    {
        if (block.Source?.Timecodes is null)
            return -1;

        var sourceTimecodes = block.Source.Timecodes.Value;
        var sourceStart = sourceTimecodes.Start.TotalMilliseconds;
        var sourceEnd = sourceTimecodes.End.TotalMilliseconds;

        int resultIndex = -1;
        var mostOverlapPct = 0.0;

        // TODO - This currently iterates over the entire list for every search.
        // Assuming that searchList is sorted by timecode, this can be optimized by
        // returning the first overlapping timecode (along with the best one) and using
        // that as the start of the next search, and stopping once a timecode with no
        // overlap is reached.
        for (int i = 0; i < searchList.Count; i++)
        {
            var searchBlock = getSearchBlock(searchList[i]);
            if (searchBlock?.Source?.Timecodes is null)
                continue;

            var searchTimecodes = searchBlock.Source.Timecodes.Value;
            var searchStart = searchTimecodes.Start.TotalMilliseconds;
            var searchEnd = searchTimecodes.End.TotalMilliseconds;
            var overlapStart = Math.Max(sourceStart, searchStart);
            var overlapEnd = Math.Min(sourceEnd, searchEnd);
            var overallStart = Math.Min(sourceStart, searchStart);
            var overallEnd = Math.Max(sourceEnd, searchEnd);
            var overlapDuration = overlapEnd - overlapStart;
            var overallDuration = overallEnd - overallStart;
            var overlapPct = overlapDuration / overallDuration;

            if (overlapPct > OverlapThreshold && overlapPct > mostOverlapPct)
            {
                resultIndex = i;
                mostOverlapPct = overlapPct;
            }
        }
        return resultIndex;
    }
}
