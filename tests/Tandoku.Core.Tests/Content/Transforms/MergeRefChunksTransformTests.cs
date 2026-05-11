namespace Tandoku.Tests.Content.Transforms;

using System.Collections.Immutable;
using Tandoku.Content;
using Tandoku.Content.Transforms;

public class MergeRefChunksTransformTests
{
    [Test]
    public async Task RefOnlyChunkMergedIntoFollowingTextChunk()
    {
        var refChunk = WithRefs(new ContentBlockChunk(), ("k", "ref-text"));
        var textChunk = new ContentBlockChunk { Text = "main" };

        var result = await Apply(refChunk, textChunk);

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("main");
        result[0].References.Should().ContainKey("k");
    }

    [Test]
    public async Task RefOnlyChunk_FollowingChunkAlreadyHasRefs_BothEmittedSeparately()
    {
        var refChunk = WithRefs(new ContentBlockChunk(), ("k", "ref-text"));
        var textChunkWithRefs = WithRefs(new ContentBlockChunk { Text = "main" }, ("other", "x"));

        var result = await Apply(refChunk, textChunkWithRefs);

        result.Should().HaveCount(2);
        result[0].HasReferencesOnly().Should().BeTrue();
        result[1].References.Should().ContainKey("other");
    }

    [Test]
    public async Task TwoRefOnlyChunksInARow_FirstEmittedAlone()
    {
        var refA = WithRefs(new ContentBlockChunk(), ("a", "1"));
        var refB = WithRefs(new ContentBlockChunk(), ("b", "2"));
        var text = new ContentBlockChunk { Text = "tail" };

        var result = await Apply(refA, refB, text);

        result.Should().HaveCount(2);
        result[0].References.Should().ContainKey("a");
        result[1].References.Should().ContainKeys("b");
        result[1].Text.Should().Be("tail");
    }

    [Test]
    public async Task TrailingRefOnlyChunk_EmittedAtEnd()
    {
        var text = new ContentBlockChunk { Text = "head" };
        var refChunk = WithRefs(new ContentBlockChunk(), ("k", "trail"));

        var result = await Apply(text, refChunk);

        result.Should().HaveCount(2);
        result[0].Text.Should().Be("head");
        result[1].HasReferencesOnly().Should().BeTrue();
    }

    [Test]
    public async Task NoRefOnlyChunks_PassThrough()
    {
        var a = new ContentBlockChunk { Text = "a" };
        var b = new ContentBlockChunk { Text = "b" };

        var result = await Apply(a, b);
        result.Select(c => c.Text).Should().Equal("a", "b");
    }

    private static ContentBlockChunk WithRefs(ContentBlockChunk chunk, params (string k, string text)[] refs)
    {
        var dict = ImmutableSortedDictionary<string, Chunk>.Empty;
        foreach (var r in refs)
            dict = dict.Add(r.k, new Chunk { Text = r.text });
        return chunk with { References = dict };
    }

    private static async Task<List<ContentBlockChunk>> Apply(params ContentBlockChunk[] chunks)
    {
        var transform = new MergeRefChunksTransform();
        var block = new ContentBlock { Chunks = chunks.ToImmutableList() };
        var transformed = await transform.TransformAsync(AsAsync(block), null!).ToList();
        return transformed.Single().Chunks.ToList();
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
