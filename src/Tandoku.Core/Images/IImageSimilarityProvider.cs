namespace Tandoku.Images;

using System.IO.Abstractions;

public interface IImageSimilarityProvider<TSignature>
    where TSignature : IImageSignature<TSignature>
{
    Task<TSignature> ComputeSignatureAsync(IFileInfo imageFile);
}

public interface IImageSignature<TSelf>
    where TSelf : IImageSignature<TSelf>
{
    double SimilarityTo(TSelf other);
}
