namespace Tandoku.Media;

using System.IO.Abstractions;
using Tandoku.Content;

public interface IImageAnalysisProvider
{
    string ImageAnalysisFileExtension { get; }

    Task<IReadOnlyCollection<Chunk>> ReadTextChunksAsync(IFileInfo imageAnalysisFile);
}
