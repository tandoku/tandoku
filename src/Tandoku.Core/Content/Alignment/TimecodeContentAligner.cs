namespace Tandoku.Content.Alignment;

using System.Collections.Generic;

public sealed class TimecodeContentAligner(string refName) : IContentAligner
{
    private const double OverlapThreshold = 0.3;

    public async IAsyncEnumerable<ContentBlock> AlignAsync(
        IAsyncEnumerable<ContentBlock> inputBlocks,
        IAsyncEnumerable<ContentBlock> refBlocks)
    {
        // This algorithm merges two block streams based on timecodes in approximately O(N) time.
        //
        // M:M (many-to-many) mapping is performed so:
        // - each block from the inputBlocks stream may be mapped to multiple blocks from the refBlock stream, and
        // - each block from the refBlocks stream may be mapped to multiple blocks from the inputBlocks stream.
        //
        // The inputBlocks stream is prioritized - all input blocks will be directly preserved in the output
        // without duplication, while refBlocks content may be repeated across multiple inputBlocks.
        //
        // Alignment happens in three passes:
        // 1) First iterate over all the inputBlocks, finding the ref block with the most overlap and assigning it
        //    to the input block, or adding the input block as standalone if no overlapping ref block exists.
        //
        // 2) Second iterate over all refBlocks that were unused, finding the input block with the most overlap and
        //    assigning the ref block to that input block, or adding the ref block as standalone if no overlapping
        //    input block exists.
        //
        // 3) Finally sort the aligned results by timecode and merge blocks for result output.

        var inputList = await inputBlocks.ToList();
        var refList = await refBlocks.ToList();
        var aligned = new List<(ContentBlock? Block, List<ContentBlock> RefBlocks)>();
        var usedRefs = new bool[refList.Count];

        // GetMostOverlappingBlock requires sorted lists
        inputList.Sort(b => b.Source, TimecodeComparer.Instance);
        refList.Sort(b => b?.Source, TimecodeComparer.Instance);

        // (1)
        var nextSearchIndex = 0;
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

        // (2)
        nextSearchIndex = 0;
        for (var i = 0; i < refList.Count; i++)
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

        // (3)
        aligned.Sort(
            n => n.Block?.Source ?? n.RefBlocks.FirstOrDefault()?.Source,
            TimecodeComparer.Instance);

        foreach (var result in aligned)
        {
            result.RefBlocks.Sort(b => b.Source, TimecodeComparer.Instance);
            yield return this.MergeBlocks(result.Block, result.RefBlocks);
        }
    }

    // Consider generalizing this functionality and moving into a base or utility class
    private ContentBlock MergeBlocks(ContentBlock? block, IEnumerable<ContentBlock> refBlocks)
    {
        block ??= new();
        foreach (var refBlock in refBlocks)
        {
            block = this.MergeRefIntoBlock(block, refBlock);
            block = this.MergeRefChunkIntoBlock(block, refBlock);
        }
        return block;
    }

    private ContentBlock MergeRefIntoBlock(ContentBlock block, ContentBlock refBlock)
    {
        if (block.References.TryGetValue(refName, out var blockRef))
        {
            // Note - not merging other properties (e.g. Image)
            blockRef = blockRef with
            {
                Source = MergeSource(blockRef.Source, refBlock.Source),
            };
        }
        else
        {
            blockRef = refBlock.ToBlock();
        }

        return block with
        {
            References = block.References.SetItem(refName, blockRef),
        };
    }

    private static BlockSource? MergeSource(BlockSource? source, BlockSource? mergeSource)
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

            // Note - not trying to merge other source properties
            return source with
            {
                Ordinal = source.Ordinal ?? mergeSource.Ordinal,
                Timecodes = mergedTimecodes ?? source.Timecodes ?? mergeSource.Timecodes,
            };
        }
        return source ?? mergeSource;
    }

    private ContentBlock MergeRefChunkIntoBlock(ContentBlock block, ContentBlock refBlock)
    {
        var chunk = block.SingleChunk();
        var refBlockChunk = refBlock.SingleChunk();

        if (chunk.References.TryGetValue(refName, out var blockRefChunk))
        {
            if (!string.IsNullOrWhiteSpace(refBlockChunk.Text))
            {
                // Note - not merging other properties (e.g. Actor, Kind)
                blockRefChunk = blockRefChunk with
                {
                    Text = $"{blockRefChunk.Text}{Environment.NewLine}{Environment.NewLine}{refBlockChunk.Text}",
                };
            }
        }
        else
        {
            blockRefChunk = refBlockChunk;
        }

        chunk = chunk with
        {
            References = chunk.References.SetItem(refName, blockRefChunk),
        };

        return block with
        {
            Chunks = [chunk],
        };
    }

    private static (int mostOverlapIndex, int nextSearchIndex) GetMostOverlappingBlock<T>(
        ContentBlock block,
        IReadOnlyList<T> searchList,
        int startIndex,
        Func<T, ContentBlock?> getSearchBlock)
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
            // TODO #6 - fix this properly (how to handle case that startIndex = -1)
            if (i < 0)
                continue;

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

    private sealed class TimecodeComparer : IComparer<BlockSource?>
    {
        internal static readonly TimecodeComparer Instance = new();

        private TimecodeComparer()
        {
        }

        public int Compare(BlockSource? x, BlockSource? y)
        {
            var cmp = Comparer<TimecodePair?>.Default.Compare(x?.Timecodes, y?.Timecodes);
            return cmp != 0 ?
                cmp :
                Comparer<int?>.Default.Compare(x?.Ordinal, y?.Ordinal);
        }
    }
}
