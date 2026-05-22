namespace Tandoku.Media;

using System.IO.Abstractions;

public interface ISerializableImageSignature<TSelf> : IImageSignature<TSelf>
    where TSelf : ISerializableImageSignature<TSelf>
{
    static abstract Task<TSelf> ReadAsync(IFileInfo cacheFile);

    Task WriteAsync(IFileInfo cacheFile);
}

public static class CachingImageSimilarityProviderExtensions
{
    public static IImageSimilarityProvider<TSignature> AddCaching<TSignature>(
        this IImageSimilarityProvider<TSignature> provider,
        string cacheFileSuffix)
        where TSignature : ISerializableImageSignature<TSignature> =>
        new CachingImageSimilarityProvider<TSignature>(provider, cacheFileSuffix);
}

public class CachingImageSimilarityProvider<TSignature>(
    IImageSimilarityProvider<TSignature> provider,
    string cacheFileSuffix) : IImageSimilarityProvider<TSignature>
    where TSignature : ISerializableImageSignature<TSignature>
{
    public async Task<TSignature> ComputeSignatureAsync(IFileInfo imageFile)
    {
        var imageDir = imageFile.Directory;
        if (imageDir is null)
            return await provider.ComputeSignatureAsync(imageFile);

        var cacheDir = imageDir.GetSubdirectory("similarity");
        var cacheFile = cacheDir.GetFile(imageFile.Name + cacheFileSuffix);

        if (cacheFile.Exists)
        {
            return await TSignature.ReadAsync(cacheFile);
        }
        else
        {
            var signature = await provider.ComputeSignatureAsync(imageFile);
            cacheDir.Create();
            await signature.WriteAsync(cacheFile);
            return signature;
        }
    }
}
