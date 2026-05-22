namespace Tandoku.Media;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;

public interface ISerializableImageSignature<TSelf> : IImageSignature<TSelf>
    where TSelf : ISerializableImageSignature<TSelf>
{
    static abstract TSelf FromJson(JsonNode node);

    JsonNode ToJson();
}

public static class CachingImageSimilarityProviderExtensions
{
    public static CachingImageSimilarityProvider<TSignature> AddCaching<TSignature>(
        this IImageSimilarityProvider<TSignature> provider,
        string cacheFileName,
        IFileSystem fileSystem)
        where TSignature : ISerializableImageSignature<TSignature> =>
        new(provider, cacheFileName, fileSystem);
}

public sealed class CachingImageSimilarityProvider<TSignature> : IImageSimilarityProvider<TSignature>
    where TSignature : ISerializableImageSignature<TSignature>
{
    private const string CacheDirectoryName = "similarity";

    private readonly IImageSimilarityProvider<TSignature> provider;
    private readonly string cacheFileName;
    private readonly ConcurrentDictionary<string, Lazy<DirectoryCache>> caches;
    private int disposed;

    public CachingImageSimilarityProvider(
        IImageSimilarityProvider<TSignature> provider,
        string cacheFileName,
        IFileSystem fileSystem)
    {
        this.provider = provider;
        this.cacheFileName = cacheFileName;
        this.caches = new(fileSystem.Path.GetComparer());
    }

    public async Task<TSignature> ComputeSignatureAsync(IFileInfo imageFile)
    {
        var imageDir = imageFile.Directory;
        if (imageDir is null)
            return await this.provider.ComputeSignatureAsync(imageFile);

        var cache = this.GetOrLoadCache(imageDir);
        var entry = cache.Signatures.GetOrAdd(
            imageFile.Name,
            name => new Lazy<Task<TSignature>>(async () =>
            {
                var signature = await this.provider.ComputeSignatureAsync(imageFile);
                cache.MarkDirty();
                return signature;
            }));
        return await entry.Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            return;

        foreach (var cache in this.caches.Values)
        {
            if (cache.IsValueCreated)
                await cache.Value.SaveAsync();
        }

        await this.provider.DisposeAsync();
    }

    private DirectoryCache GetOrLoadCache(IDirectoryInfo imageDir) =>
        this.caches.GetOrAdd(imageDir.FullName, _ => new Lazy<DirectoryCache>(() =>
        {
            var cacheDir = imageDir.GetSubdirectory(CacheDirectoryName);
            var cacheFile = cacheDir.GetFile(this.cacheFileName);
            return DirectoryCache.Load(cacheFile);
        })).Value;

    private sealed class DirectoryCache
    {
        private const string ImagesPropertyName = "images";
        private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

        private readonly IFileInfo cacheFile;
        private int dirty;

        private DirectoryCache(
            IFileInfo cacheFile,
            ConcurrentDictionary<string, Lazy<Task<TSignature>>> signatures)
        {
            this.cacheFile = cacheFile;
            this.Signatures = signatures;
        }

        public ConcurrentDictionary<string, Lazy<Task<TSignature>>> Signatures { get; }

        public void MarkDirty() => Interlocked.Exchange(ref this.dirty, 1);

        public static DirectoryCache Load(IFileInfo cacheFile)
        {
            var comparer = cacheFile.FileSystem.Path.GetComparer();
            var signatures = new ConcurrentDictionary<string, Lazy<Task<TSignature>>>(comparer);
            if (cacheFile.Exists)
            {
                using var stream = cacheFile.OpenRead();
                var root = JsonNode.Parse(stream);
                if (root?[ImagesPropertyName] is JsonObject images)
                {
                    foreach (var (name, node) in images)
                    {
                        if (node is null)
                            continue;
                        var signature = TSignature.FromJson(node);
                        signatures[name] = new Lazy<Task<TSignature>>(Task.FromResult(signature));
                    }
                }
            }
            return new DirectoryCache(cacheFile, signatures);
        }

        public async Task SaveAsync()
        {
            if (this.dirty == 0)
                return;

            var images = new JsonObject();
            foreach (var (name, entry) in this.Signatures.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!entry.IsValueCreated || !entry.Value.IsCompletedSuccessfully)
                    continue;
                images[name] = entry.Value.Result.ToJson();
            }
            var root = new JsonObject { [ImagesPropertyName] = images };

            this.cacheFile.Directory?.Create();
            await using var stream = this.cacheFile.Create();
            await using var writer = new Utf8JsonWriter(stream, WriterOptions);
            root.WriteTo(writer);
        }
    }
}
