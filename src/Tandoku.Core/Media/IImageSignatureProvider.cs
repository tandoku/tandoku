namespace Tandoku.Media;

using System.IO.Abstractions;

public interface IImageSignatureProvider<T> : IAsyncDisposable
    where T : IImageSignature<T>
{
    Task<T> ComputeSignatureAsync(IFileInfo imageFile);
}

public interface IImageSignature<TSelf>
    where TSelf : IImageSignature<TSelf>
{
    double SimilarityTo(TSelf other);
}
