namespace Tandoku.Tests.Content;

using System.Collections.Immutable;
using Tandoku.Content;

public class ContentBlockTests
{
    [Test]
    public void SingleChunk_Empty_ReturnsEmpty()
    {
        new ContentBlock().SingleChunk().Should().BeSameAs(ContentBlockChunk.Empty);
    }

    [Test]
    public void SingleChunk_OneChunk_ReturnsIt()
    {
        var chunk = new ContentBlockChunk { Text = "hi" };
        new ContentBlock { Chunks = ImmutableList.Create(chunk) }.SingleChunk()
            .Should().BeSameAs(chunk);
    }

    [Test]
    public void SingleChunk_MultipleChunks_Throws()
    {
        var block = new ContentBlock
        {
            Chunks = ImmutableList.Create(
                new ContentBlockChunk { Text = "a" },
                new ContentBlockChunk { Text = "b" }),
        };
        block.Invoking(b => b.SingleChunk()).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToBlock_StripsContentBlockProperties()
    {
        var block = new ContentBlock
        {
            Id = "x",
            Source = new BlockSource { Ordinal = 1 },
            Audio = new ContentBlockAudio { Name = "a.wav" },
            References = ImmutableSortedDictionary<string, Block>.Empty.Add("k", new Block()),
            Chunks = ImmutableList.Create(new ContentBlockChunk { Text = "hello" }),
        };
        var bare = block.ToBlock();
        bare.Source.Should().Be(block.Source);
        bare.GetType().Should().Be<Block>();
    }

    [Test]
    public void HasReferencesOnly_TrueWhenOnlyReferences()
    {
        var chunk = new ContentBlockChunk
        {
            References = ImmutableSortedDictionary<string, Chunk>.Empty.Add("r", new Chunk { Text = "x" }),
        };
        chunk.HasReferencesOnly().Should().BeTrue();
    }

    [Test]
    public void HasReferencesOnly_FalseWhenChunkHasText()
    {
        var chunk = new ContentBlockChunk
        {
            Text = "x",
            References = ImmutableSortedDictionary<string, Chunk>.Empty.Add("r", new Chunk { Text = "y" }),
        };
        chunk.HasReferencesOnly().Should().BeFalse();
    }

    [Test]
    public void Json_RoundTrip_PreservesData()
    {
        var block = new ContentBlock
        {
            Id = "id1",
            Source = new BlockSource
            {
                Ordinal = 5,
                Timecodes = new TimecodePair(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                Note = "note",
            },
            Chunks = ImmutableList.Create(new ContentBlockChunk
            {
                Text = "テスト",
                Role = ChunkRole.OnScreenText,
            }),
        };

        var json = block.ToJsonString();
        // Japanese characters should not be escaped (UnsafeRelaxedJsonEscaping configured).
        json.Should().Contain("テスト");
        // OnScreenText enum serialized as kebab-case via [JsonStringEnumMemberName].
        json.Should().Contain("\"role\":\"on-screen-text\"");

        var parsed = ContentBlock.DeserializeJson(json);
        parsed.Should().BeEquivalentTo(block);
    }
}

public class TimecodePairTests
{
    [Test]
    public void Duration_IsEndMinusStart()
    {
        var pair = new TimecodePair(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        pair.Duration.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Test]
    public void ToString_FormatsAsHumanReadable()
    {
        var pair = new TimecodePair(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        pair.ToString().Should().Contain("-->");
    }

    [Test]
    public void CompareTo_ComparesByStartThenEnd()
    {
        var a = new TimecodePair(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        var b = new TimecodePair(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
        var c = new TimecodePair(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
        b.CompareTo(c).Should().BeNegative();
    }
}
