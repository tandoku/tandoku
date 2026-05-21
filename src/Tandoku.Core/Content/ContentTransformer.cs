namespace Tandoku.Content;

using System.IO.Abstractions;
using Tandoku.Serialization;

public sealed class ContentTransformer
{
    private readonly IFileSystem fileSystem;
    private readonly string inputPath;
    private readonly string outputPath;

    public ContentTransformer(string inputPath, string outputPath, IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.inputPath = inputPath;
        this.outputPath = outputPath;
    }

    public async Task TransformAsync(IContentBlockTransform transform)
    {
        var inputDir = this.fileSystem.GetDirectory(this.inputPath);
        var outputDir = this.fileSystem.GetDirectory(this.outputPath);
        outputDir.Create();
        var inputFiles = inputDir.EnumerateContentFiles();
        if (transform.ParallelProcessing)
        {
            await Parallel.ForEachAsync(inputFiles, async (inputFile, _) =>
                await TransformFileAsync(inputFile, outputDir, transform));
        }
        else
        {
            foreach (var inputFile in inputFiles)
                await TransformFileAsync(inputFile, outputDir, transform);
        }
    }

    private static Task TransformFileAsync(IFileInfo inputFile, IDirectoryInfo outputDir, IContentBlockTransform transform)
    {
        var outputFile = outputDir.GetFile(inputFile.Name);
        var blocks = YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile);
        return YamlSerializer.WriteStreamAsync(
            outputFile,
            transform.TransformAsync(blocks, inputFile));
    }

    public Task TransformAsync(Func<ContentBlockChunk, ContentBlockChunk> transformChunk) =>
        this.TransformAsync(new ChunkTransform(transformChunk));

    private sealed class ChunkTransform(Func<ContentBlockChunk, ContentBlockChunk> transformChunk) :
        ContentBlockTransform
    {
        protected override ContentBlockChunk? TransformChunk(ContentBlockChunk chunk) =>
            transformChunk(chunk);
    }
}
