namespace Tandoku.Images;

using System.IO.Abstractions;

public interface IImageSimilarityProvider
{
    Task<IImageSignature> ComputeSignatureAsync(IFileInfo imageFile);
}

public interface IImageSignature
{
    double SimilarityTo(IImageSignature other);
}
