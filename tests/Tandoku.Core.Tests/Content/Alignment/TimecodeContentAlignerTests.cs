namespace Tandoku.Tests.Content.Alignment;

using System.Collections.Immutable;
using Tandoku.Content;
using Tandoku.Content.Alignment;

public class TimecodeContentAlignerTests
{
    [Test]
    public async Task BlocksWithoutTimecodes_NotAligned_PreservedInOrder()
    {
        var aligner = new TimecodeContentAligner("ref");
        var input = AsAsync(BlockText("a", null), BlockText("b", null));
        var refs = AsAsync(BlockText("R", null));

        var aligned = await aligner.AlignAsync(input, refs).ToList();
        aligned.Should().HaveCount(3); // 2 input + 1 leftover ref
    }

    [Test]
    public async Task OverlappingRefBlock_MergedIntoInputBlockReferences()
    {
        var aligner = new TimecodeContentAligner("subs");
        var input = AsAsync(BlockWithTimecodes("primary", 0, 1000));
        var refs = AsAsync(BlockWithTimecodes("ref", 100, 800));

        var aligned = await aligner.AlignAsync(input, refs).ToList();

        aligned.Should().HaveCount(1);
        var block = aligned[0];
        block.References.Should().ContainKey("subs");
        block.SingleChunk().References.Should().ContainKey("subs");
        block.SingleChunk().References["subs"].Text.Should().Be("ref");
    }

    [Test]
    public async Task NonOverlappingRefBlock_EmittedAsStandaloneBlock()
    {
        var aligner = new TimecodeContentAligner("subs");
        var input = AsAsync(BlockWithTimecodes("primary", 0, 1000));
        var refs = AsAsync(BlockWithTimecodes("orphan", 5000, 6000));

        var aligned = await aligner.AlignAsync(input, refs).ToList();
        aligned.Should().HaveCount(2);
        aligned[0].SingleChunk().Text.Should().Be("primary");
        aligned[1].References.Should().ContainKey("subs");
        aligned[1].SingleChunk().References["subs"].Text.Should().Be("orphan");
    }

    [Test]
    public async Task MultipleRefBlocks_OverlappingSameInput_TextIsConcatenated()
    {
        var aligner = new TimecodeContentAligner("subs");
        var input = AsAsync(BlockWithTimecodes("primary", 0, 2000));
        var refs = AsAsync(
            BlockWithTimecodes("first", 100, 800),
            BlockWithTimecodes("second", 900, 1900));

        var aligned = await aligner.AlignAsync(input, refs).ToList();
        aligned.Should().HaveCount(1);
        var refText = aligned[0].SingleChunk().References["subs"].Text;
        refText.Should().Contain("first");
        refText.Should().Contain("second");
    }

    private static ContentBlock BlockText(string text, BlockSource? source) =>
        new()
        {
            Source = source,
            Chunks = ImmutableList.Create(new ContentBlockChunk { Text = text }),
        };

    private static ContentBlock BlockWithTimecodes(string text, int startMs, int endMs) =>
        BlockText(text, new BlockSource
        {
            Timecodes = new TimecodePair(
                TimeSpan.FromMilliseconds(startMs),
                TimeSpan.FromMilliseconds(endMs)),
        });

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
