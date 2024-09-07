namespace Tandoku.Content;

using System.IO.Abstractions;
using Tandoku.Content.Alignment;
using Tandoku.Serialization;

public sealed class ContentMerger
{
    private readonly IFileSystem fileSystem;

    public ContentMerger(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task MergeAsync(string inputPath, string refPath, string outputPath, IContentAligner aligner)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var refDir = this.fileSystem.GetDirectory(refPath);
        var outputDir = this.fileSystem.GetDirectory(outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateContentFiles())
        {
            var refFile = refDir.GetFile(inputFile.Name);
            var outputFile = outputDir.GetFile(inputFile.Name);
            var inputBlocks = YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile);
            var refBlocks = YamlSerializer.ReadStreamAsync<ContentBlock>(refFile);
            await YamlSerializer.WriteStreamAsync(outputFile, aligner.AlignAsync(inputBlocks, refBlocks));
        }
    }
}
