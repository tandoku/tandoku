namespace Tandoku.Tests.Content.Transforms;

using System.Collections.Immutable;
using Tandoku.Content;
using Tandoku.Content.Transforms;

public class RemoveLowConfidenceTextTransformTests
{
    [Test]
    public void NoImage_ChunkUnchanged()
    {
        var transform = new RemoveLowConfidenceTextTransform(0.8);
        var chunk = new ContentBlockChunk { Text = "hello" };
        Apply(transform, chunk).Should().BeSameAs(chunk);
    }

    [Test]
    public void HighConfidence_ChunkUnchanged()
    {
        var transform = new RemoveLowConfidenceTextTransform(0.5);
        var chunk = new ContentBlockChunk
        {
            Text = "hi",
            Image = new ChunkImage
            {
                TextSpans = ImmutableList.Create(
                    new ImageTextSpan { Text = "hi", Confidence = 0.9 }),
            },
        };
        Apply(transform, chunk).Should().BeSameAs(chunk);
    }

    [Test]
    public void LowConfidenceFirstSpan_DroppedAndReplacedWithSpace()
    {
        var transform = new RemoveLowConfidenceTextTransform(0.5);
        var chunk = new ContentBlockChunk
        {
            Text = "ab cd",
            Image = new ChunkImage
            {
                TextSpans = ImmutableList.Create(
                    new ImageTextSpan { Text = "ab", Confidence = 0.1 },
                    new ImageTextSpan { Text = "cd", Confidence = 0.9 }),
            },
        };

        var result = Apply(transform, chunk);
        result.Should().NotBeNull();
        result!.Text.Should().Be("cd");
        result.Image!.TextSpans.Should().HaveCount(1);
        result.Image.TextSpans[0].Text.Should().Be("cd");
    }

    [Test]
    public void AllSpansLowConfidence_ChunkRemoved()
    {
        var transform = new RemoveLowConfidenceTextTransform(0.5);
        var chunk = new ContentBlockChunk
        {
            Text = "ab cd",
            Image = new ChunkImage
            {
                TextSpans = ImmutableList.Create(
                    new ImageTextSpan { Text = "ab", Confidence = 0.1 },
                    new ImageTextSpan { Text = "cd", Confidence = 0.2 }),
            },
        };

        Apply(transform, chunk).Should().BeNull();
    }

    private static ContentBlockChunk? Apply(ContentBlockTransform transform, ContentBlockChunk chunk)
    {
        var block = new ContentBlock { Chunks = ImmutableList.Create(chunk) };
        var transformed = TransformBlock(transform, block);
        return transformed?.Chunks.SingleOrDefault();
    }

    private static ContentBlock? TransformBlock(ContentBlockTransform transform, ContentBlock block)
    {
        // Use the public TransformAsync entry point
        var result = transform.TransformAsync(AsAsync(block), null!).ToList().GetAwaiter().GetResult();
        return result.SingleOrDefault();
    }

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
