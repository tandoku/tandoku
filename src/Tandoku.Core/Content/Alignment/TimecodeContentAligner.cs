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
        var inputList = await inputBlocks.ToList();
        var refList = await refBlocks.ToList<TextBlock?>();
        var aligned = new List<(TextBlock? Block, List<TextBlock> RefBlocks)>();

        // GetMostOverlappingBlock requires sorted lists
        inputList.Sort(b => b.Source?.Timecodes);
        refList.Sort(b => b?.Source?.Timecodes);

        int nextSearchIndex = 0;
        foreach (var block in inputList)
        {
            (var overlapIndex, nextSearchIndex) =
                GetMostOverlappingBlock(block, refList, nextSearchIndex, b => b);

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

        nextSearchIndex = 0;
        foreach (var refBlock in refList)
        {
            if (refBlock is null)
                continue;

            (var overlapIndex, nextSearchIndex) =
                GetMostOverlappingBlock(refBlock, aligned, nextSearchIndex, n => n.Block);

            if (overlapIndex >= 0)
            {
                aligned[overlapIndex].RefBlocks.Add(refBlock);
            }
            else
            {
                aligned.Add((null, [refBlock]));
            }
        }

        aligned.Sort(
            n => n.Block?.Source?.Timecodes ??
                 n.RefBlocks.FirstOrDefault()?.Source?.Timecodes);

        foreach (var result in aligned)
        {
            yield return this.MergeBlocks(
                result.Block,
                result.RefBlocks.OrderBy(b => b.Source?.Timecodes));
        }
    }

    private static (int mostOverlapIndex, int nextSearchIndex) GetMostOverlappingBlock<T>(
        TextBlock block,
        IReadOnlyList<T> searchList,
        int startIndex,
        Func<T, TextBlock?> getSearchBlock)
    {
        if (block.Source?.Timecodes is null)
            return (-1, startIndex);

        var sourceTimecodes = block.Source.Timecodes.Value;
        var sourceStart = sourceTimecodes.Start.TotalMilliseconds;
        var sourceEnd = sourceTimecodes.End.TotalMilliseconds;

        int nextSearchIndex = -1;
        int resultIndex = -1;
        var mostOverlapPct = 0.0;

        for (int i = startIndex; i < searchList.Count; i++)
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

            if (nextSearchIndex < 0 && searchEnd >= sourceStart)
                nextSearchIndex = i;
            else if (searchStart >= sourceEnd)
                break;

            if (overlapPct > OverlapThreshold && overlapPct > mostOverlapPct)
            {
                resultIndex = i;
                mostOverlapPct = overlapPct;
            }
        }
        return (resultIndex, nextSearchIndex);
    }
}
