namespace Tandoku.Content;

using System.Collections.Immutable;
using System.IO.Abstractions;

public interface IContentBlockTransform
{
    IAsyncEnumerable<ContentBlock> TransformAsync(IAsyncEnumerable<ContentBlock> blocks, IFileInfo file);
}

public abstract class ContentBlockTransform : IContentBlockTransform
{
    public async IAsyncEnumerable<ContentBlock> TransformAsync(IAsyncEnumerable<ContentBlock> blocks, IFileInfo file)
    {
        await foreach (var block in blocks)
        {
            var newBlock = this.TransformBlock(block);
            if (newBlock is not null)
                yield return newBlock;
        }
    }

    protected virtual ContentBlock? TransformBlock(ContentBlock block)
    {
        var newChunks = this.TransformChunks(block.Chunks).ToImmutableList();

        return block with
        {
            Chunks = newChunks
        };
    }

    protected virtual IEnumerable<ContentBlockChunk> TransformChunks(IEnumerable<ContentBlockChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var newChunk = this.TransformChunk(chunk);
            if (newChunk is not null)
                yield return newChunk;
        }
    }

    protected virtual ContentBlockChunk? TransformChunk(ContentBlockChunk chunk)
    {
        return chunk;
    }
}
