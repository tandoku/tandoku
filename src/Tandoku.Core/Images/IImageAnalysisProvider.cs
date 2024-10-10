namespace Tandoku.Images;

using System.IO.Abstractions;
using Tandoku.Content;

public interface IImageAnalysisProvider
{
    string ImageAnalysisFileExtension { get; }

    IEnumerable<TextBlock> ReadTextBlocks(IFileInfo imageAnalysisFile);
}
