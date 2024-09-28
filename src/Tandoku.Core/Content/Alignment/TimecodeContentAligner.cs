namespace Tandoku.Content.Alignment;

using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
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
        var refList = await refBlocks.ToList();
        var aligned = new List<(TextBlock? Block, List<TextBlock> RefBlocks)>();
        var usedRefs = new bool[refList.Count];

        // GetMostOverlappingBlock requires sorted lists
        inputList.Sort(b => b.Source, TimecodeComparer.Instance);
        refList.Sort(b => b?.Source, TimecodeComparer.Instance);

        int nextSearchIndex = 0;
        foreach (var block in inputList)
        {
            (var overlapIndex, nextSearchIndex) =
                GetMostOverlappingBlock(block, refList, nextSearchIndex, b => b);

            if (overlapIndex >= 0)
            {
                var refBlock = refList[overlapIndex];
                aligned.Add((block, [refBlock]));
                usedRefs[overlapIndex] = true;
            }
            else
            {
                aligned.Add((block, []));
            }
        }

        nextSearchIndex = 0;
        for (int i = 0; i < refList.Count; i++)
        {
            if (usedRefs[i])
                continue;

            var refBlock = refList[i];

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
            n => n.Block?.Source ?? n.RefBlocks.FirstOrDefault()?.Source,
            TimecodeComparer.Instance);

        foreach (var result in aligned)
        {
            result.RefBlocks.Sort(b => b.Source, TimecodeComparer.Instance);
            yield return this.MergeBlocks(result.Block, result.RefBlocks);
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
        var sourceDuration = sourceEnd - sourceStart;

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
            var overlapDuration = overlapEnd - overlapStart;
            var overlapPct = overlapDuration / sourceDuration;

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

    private sealed class TimecodeComparer : IComparer<ContentSource?>
    {
        internal static readonly TimecodeComparer Instance = new();

        private TimecodeComparer()
        {
        }

        public int Compare(ContentSource? x, ContentSource? y)
        {
            var cmp = Comparer<TimecodePair?>.Default.Compare(x?.Timecodes, y?.Timecodes);
            return cmp != 0 ?
                cmp :
                Comparer<int?>.Default.Compare(x?.Ordinal, y?.Ordinal);
        }
    }
}
