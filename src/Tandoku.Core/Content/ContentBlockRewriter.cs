﻿namespace Tandoku.Content;

using System.Collections.Immutable;
using System.IO.Abstractions;

public class ContentBlockRewriter : ContentBlockVisitor<ContentBlock?>, IContentBlockTransform
{
    protected IFileInfo? CurrentFile { get; private set; }

    public override ContentBlock? Visit(TextBlock block) => block;

    public override ContentBlock? Visit(CompositeBlock block)
    {
        return block with
        {
            Blocks = this.VisitNestedBlocks(block.Blocks).ToImmutableList(),
        };
    }

    public virtual IEnumerable<TextBlock> VisitNestedBlocks(IEnumerable<TextBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var newBlock = (TextBlock?)block.Accept(this);
            if (newBlock is not null)
                yield return newBlock;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "Implementation is trivial and not needed by derived classes.")]
    ContentBlock? IContentBlockTransform.Transform(ContentBlock block, IFileInfo file)
    {
        this.CurrentFile = file;
        try
        {
            return block.Accept(this);
        }
        finally
        {
            this.CurrentFile = null;
        }
    }
}
