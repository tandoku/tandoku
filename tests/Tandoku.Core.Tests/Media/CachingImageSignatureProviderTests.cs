namespace Tandoku.Tests.Media;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json.Nodes;
using Tandoku.Media;

public class CachingImageSignatureProviderTests
{
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public async Task ComputesAndCachesSignature_WrittenToJsonOnDispose()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        var inner = new RecordingProvider { ["a.png"] = 0x42UL };

        var provider = inner.AddCaching("hashes.json", this.mockFs);
        await using (provider)
        {
            var sig = await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));
            sig.Hash.Should().Be(0x42UL);
        }

        var cacheFile = this.Fs.GetFile("/imgs/signature/hashes.json");
        cacheFile.Exists.Should().BeTrue();
        var root = JsonNode.Parse(cacheFile.OpenRead())!;
        root["images"]!["a.png"]!.GetValue<string>().Should().Be("0000000000000042");
        inner.CallCount.Should().Be(1);
    }

    [Test]
    public async Task SecondCallForSameImage_UsesMemoryCache_DoesNotRecomputeOrRewrite()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        var inner = new RecordingProvider { ["a.png"] = 1UL };

        await using (var provider = inner.AddCaching("hashes.json", this.mockFs))
        {
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));
        }

        inner.CallCount.Should().Be(1);
    }

    [Test]
    public async Task ExistingCacheFile_LoadedOnFirstAccess_NoRecompute()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/imgs/signature/hashes.json", new MockFileData(
            """{"images":{"a.png":"0000000000000063"}}"""));
        var inner = new RecordingProvider();

        await using var provider = inner.AddCaching("hashes.json", this.mockFs);
        var sig = await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));

        sig.Hash.Should().Be(99UL);
        inner.CallCount.Should().Be(0);
    }

    [Test]
    public async Task NewSignature_MergedWithExistingCacheEntries_OnDispose()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/imgs/b.png", new MockFileData("b"));
        this.mockFs.AddFile("/imgs/signature/hashes.json", new MockFileData(
            """{"images":{"a.png":"0000000000000001"}}"""));
        var inner = new RecordingProvider { ["b.png"] = 2UL };

        await using (var provider = inner.AddCaching("hashes.json", this.mockFs))
        {
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/b.png"));
        }

        var root = JsonNode.Parse(this.Fs.GetFile("/imgs/signature/hashes.json").OpenRead())!;
        root["images"]!["a.png"]!.GetValue<string>().Should().Be("0000000000000001");
        root["images"]!["b.png"]!.GetValue<string>().Should().Be("0000000000000002");
    }

    [Test]
    public async Task NoNewSignatures_CacheFileNotRewritten()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        const string original = """{"images":{"a.png":"0000000000000007"}}""";
        this.mockFs.AddFile("/imgs/signature/hashes.json", new MockFileData(original));
        var inner = new RecordingProvider();

        await using (var provider = inner.AddCaching("hashes.json", this.mockFs))
        {
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs/a.png"));
        }

        this.mockFs.File.ReadAllText("/imgs/signature/hashes.json").Should().Be(original);
    }

    [Test]
    public async Task SeparateImageDirectories_GetSeparateCacheFiles()
    {
        this.mockFs.AddFile("/imgs1/a.png", new MockFileData("a"));
        this.mockFs.AddFile("/imgs2/a.png", new MockFileData("a"));
        var inner = new RecordingProvider { ["a.png"] = 5UL };

        await using (var provider = inner.AddCaching("hashes.json", this.mockFs))
        {
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs1/a.png"));
            await provider.ComputeSignatureAsync(this.Fs.GetFile("/imgs2/a.png"));
        }

        this.Fs.GetFile("/imgs1/signature/hashes.json").Exists.Should().BeTrue();
        this.Fs.GetFile("/imgs2/signature/hashes.json").Exists.Should().BeTrue();
    }

    [Test]
    public async Task ConcurrentCallsForSameImage_ComputeOnlyOnce()
    {
        this.mockFs.AddFile("/imgs/a.png", new MockFileData("a"));
        var inner = new SlowProvider(TimeSpan.FromMilliseconds(50)) { ["a.png"] = 0xABUL };

        await using var provider = inner.AddCaching("hashes.json", this.mockFs);
        var file = this.Fs.GetFile("/imgs/a.png");

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => provider.ComputeSignatureAsync(file)))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(s => s.Hash.Should().Be(0xABUL));
        inner.CallCount.Should().Be(1);
    }

    private class RecordingProvider : Dictionary<string, ulong>, IImageSignatureProvider<AverageHashImageSignature>
    {
        private int callCount;

        public int CallCount => this.callCount;

        public virtual Task<AverageHashImageSignature> ComputeSignatureAsync(IFileInfo imageFile)
        {
            Interlocked.Increment(ref this.callCount);
            return Task.FromResult(new AverageHashImageSignature(this[imageFile.Name]));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SlowProvider(TimeSpan delay) : RecordingProvider
    {
        public override async Task<AverageHashImageSignature> ComputeSignatureAsync(IFileInfo imageFile)
        {
            await Task.Delay(delay);
            return await base.ComputeSignatureAsync(imageFile);
        }
    }
}
