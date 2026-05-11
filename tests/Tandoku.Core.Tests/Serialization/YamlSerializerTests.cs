namespace Tandoku.Tests.Serialization;

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Content;
using Tandoku.Serialization;

public class YamlSerializerTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    private sealed record SimpleDoc : IYamlSerializable<SimpleDoc>
    {
        public required string Name { get; init; }
        public int Count { get; init; }
        public IImmutableList<string> Tags { get; init; } = ImmutableList<string>.Empty;
    }

    private sealed record StreamItem : IYamlStreamSerializable<StreamItem>
    {
        public required int Index { get; init; }
        public string? Label { get; init; }
    }

    private static Task<T> ReadAsync<T>(TextReader reader) where T : IYamlSerializable<T> =>
        T.ReadYamlAsync(reader);

    private static Task WriteAsync<T>(T doc, TextWriter writer) where T : IYamlSerializable<T> =>
        doc.WriteYamlAsync(writer);

    private static Task<T> ReadAsync<T>(IFileInfo file) where T : IYamlSerializable<T> =>
        T.ReadYamlAsync(file);

    private static Task WriteAsync<T>(T doc, IFileInfo file) where T : IYamlSerializable<T> =>
        doc.WriteYamlAsync(file);

    [Test]
    public async Task IYamlSerializable_RoundTripsViaTextWriter()
    {
        var doc = new SimpleDoc { Name = "hello", Count = 3, Tags = ImmutableList.Create("a", "b") };
        using var writer = new StringWriter();
        await WriteAsync(doc, writer);
        var yaml = writer.ToString();

        yaml.Should().Contain("name: hello");
        yaml.Should().Contain("count: 3");

        var parsed = await ReadAsync<SimpleDoc>(new StringReader(yaml));
        parsed.Should().BeEquivalentTo(doc);
    }

    [Test]
    public async Task IYamlSerializable_RoundTripsViaFile()
    {
        var doc = new SimpleDoc { Name = "fileDoc", Count = 7 };
        this.mockFs.AddDirectory("/work");
        var file = this.Fs.GetFile("/work/doc.yaml");

        await WriteAsync(doc, file);
        file.Exists.Should().BeTrue();

        var parsed = await ReadAsync<SimpleDoc>(file);
        parsed.Should().BeEquivalentTo(doc);
    }

    [Test]
    public async Task IYamlStreamSerializable_RoundTrip()
    {
        this.mockFs.AddDirectory("/work");
        var file = this.Fs.GetFile("/work/items.yaml");
        var items = AsAsync(
            new StreamItem { Index = 1, Label = "one" },
            new StreamItem { Index = 2, Label = "two" });

        await YamlSerializer.WriteStreamAsync(file, items);

        // Multiple documents must be separated by ---
        var yaml = file.OpenText().ReadToEnd();
        yaml.Should().Contain("---");
        yaml.Should().MatchRegex(@"index:\s*1");
        yaml.Should().MatchRegex(@"index:\s*2");

        var read = await YamlSerializer.ReadStreamAsync<StreamItem>(file).ToList();
        read.Should().HaveCount(2);
        read[0].Should().BeEquivalentTo(new StreamItem { Index = 1, Label = "one" });
        read[1].Should().BeEquivalentTo(new StreamItem { Index = 2, Label = "two" });
    }

    [Test]
    public async Task IYamlStreamSerializable_BadDocument_ThrowsInvalidDataExceptionWithFileContext()
    {
        this.mockFs.AddFile("/work/items.yaml", new MockFileData(
            "index: not-a-number\nlabel: bad\n"));
        var file = this.Fs.GetFile("/work/items.yaml");

        Func<Task> read = async () =>
        {
            await foreach (var _ in YamlSerializer.ReadStreamAsync<StreamItem>(file))
            {
            }
        };

        var ex = await read.Should().ThrowAsync<InvalidDataException>();
        ex.Which.Message.Should().Contain("items.yaml");
    }

    [Test]
    public async Task FlowStyleEventEmitter_TimecodePair_AndImageTextSpan_UsesFlowStyle()
    {
        // ContentBlock containing a TimecodePair and an ImageTextSpan exercise
        // FlowStyleEventEmitter's UseFlowStyle predicate.
        var block = new ContentBlock
        {
            Source = new BlockSource
            {
                Timecodes = new TimecodePair(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
            },
            Chunks = ImmutableList.Create(new ContentBlockChunk
            {
                Text = "テスト",
                Image = new ChunkImage
                {
                    TextSpans = ImmutableList.Create(
                        new ImageTextSpan { Text = "テスト", Confidence = 0.9 }),
                },
            }),
        };
        using var writer = new StringWriter();
        await WriteStreamAsync(writer, AsAsync(block));
        var yaml = writer.ToString();

        // Flow style means TimecodePair and ImageTextSpan are inline { ... }
        yaml.Should().Contain("timecodes: {");
        yaml.Should().Contain("text: テスト, confidence: 0.9}");
    }

    private static Task WriteStreamAsync<T>(TextWriter writer, IAsyncEnumerable<T> items)
        where T : IYamlStreamSerializable<T> =>
        T.WriteYamlAsync(writer, items);

    private static async IAsyncEnumerable<T> AsAsync<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
