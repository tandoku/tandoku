namespace Tandoku.Media;

using System.IO.Abstractions;

public interface IImageSimilarityProvider<TSignature> : IAsyncDisposable
    where TSignature : IImageSignature<TSignature>
{
    Task<TSignature> ComputeSignatureAsync(IFileInfo imageFile);
}

public interface IImageSignature<TSelf>
    where TSelf : IImageSignature<TSelf>
{
    double SimilarityTo(TSelf other);
}
