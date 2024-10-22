namespace Tandoku.Content.Transforms;

using System.Collections.Generic;

public sealed class MergeRefChunksTransform : ContentBlockTransform
{
    protected override IEnumerable<ContentBlockChunk> TransformChunks(IEnumerable<ContentBlockChunk> chunks)
    {
        // This transform merges a reference-only chunk with a following chunk that does not have references.
        // This can be used to merge chunks produced by ImportMediaTransform+ImportImageTextTransform potentially
        // running other transforms in between to remove unwanted text.
        // This is a very blunt heuristic for merging chunks - consider using an LLM/embedding step in the future
        // to determine appropriate text to match (and optionally discard other image text).

        ContentBlockChunk? refChunk = null;
        foreach (var chunk in chunks)
        {
            if (chunk.HasReferencesOnly())
            {
                if (refChunk is not null)
                    yield return refChunk;

                refChunk = chunk;
            }
            else if (refChunk is not null)
            {
                if (chunk.References.Count == 0)
                {
                    yield return chunk with
                    {
                        References = refChunk.References,
                    };
                }
                else
                {
                    yield return refChunk;
                    yield return chunk;
                }

                refChunk = null;
            }
            else
            {
                yield return chunk;
            }
        }

        if (refChunk is not null)
            yield return refChunk;
    }
}
