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
        foreach (var inputFile in inputDir.EnumerateContentFiles())
        {
            var outputFile = outputDir.GetFile(inputFile.Name);
            await YamlSerializer.WriteStreamAsync(outputFile, TransformBlocksAsync(inputFile));
        }

        async IAsyncEnumerable<ContentBlock> TransformBlocksAsync(IFileInfo inputFile)
        {
            await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile))
            {
                var newBlock = transform.Transform(block, inputFile);
                if (newBlock is not null)
                    yield return newBlock;
            }
        }
    }

    public Task TransformAsync(Func<TextBlock, TextBlock?> transform) =>
        this.TransformAsync(new TextBlockRewriter(transform));

    private sealed class TextBlockRewriter(Func<TextBlock, TextBlock?> transform) : ContentBlockRewriter
    {
        public override ContentBlock? Visit(TextBlock block) => transform(block);
    }
}

public interface IContentBlockTransform
{
    ContentBlock? Transform(ContentBlock block, IFileInfo file);
}
