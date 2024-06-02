namespace Tandoku.Content;

using System.IO.Abstractions;
using Tandoku.Serialization;

public sealed class ContentTransformer
{
    private readonly IFileSystem fileSystem;

    public ContentTransformer(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task Transform(string inputPath, string outputPath, IContentBlockTransform transform)
    {
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var outputDir = this.fileSystem.GetDirectory(outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateFiles("*.content.yaml")) // TODO share with ContentIndexBuilder
        {
            var outputFile = outputDir.GetFile(inputFile.Name);
            await YamlSerializer.WriteStreamAsync(outputFile, TransformBlocks(inputFile));
        }

        async IAsyncEnumerable<ContentBlock> TransformBlocks(IFileInfo inputFile)
        {
            await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile))
            {
                var newBlock = transform.Transform(block);
                if (newBlock is not null)
                    yield return newBlock;
            }
        }
    }

    public Task Transform(string inputPath, string outputPath, Func<TextBlock, TextBlock?> transform) =>
        this.Transform(inputPath, outputPath, new TextBlockRewriter(transform));

    private sealed class TextBlockRewriter(Func<TextBlock, TextBlock?> transform) : ContentBlockRewriter
    {
        public override ContentBlock? Visit(TextBlock block) => transform(block);
    }
}

public interface IContentBlockTransform
{
    ContentBlock? Transform(ContentBlock block);
}
