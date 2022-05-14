namespace Tandoku;

using Markdig.Syntax;
using Markdig.Syntax.Inlines;

internal static class SplitResult
{
    internal static SplitResult<T> Create<T>(
        bool split,
        string? replacement = null,
        bool consumeFollowingContent = false,
        T? metadata = default)
    {
        return new SplitResult<T>(
            split,
            replacement,
            consumeFollowingContent,
            metadata);
    }
}

internal record class SplitResult<T>(
    bool Split,
    string? Replacement,
    bool ConsumeFollowingContent,
    T? Metadata);

internal static class MarkdownDocumentSplitter
{
    internal static IEnumerable<(MarkdownDocument Markdown, T? Metadata)> SplitConditionallyByLines<T>(
        MarkdownDocument document,
        Func<string, SplitResult<T>> splitterFunction)
    {
        var currentDoc = new MarkdownDocument();
        var currentLine = new ContainerInline();
        var previousLines = new ContainerInline();
        T? currentMetadata = default;

        bool anySplits = false;

        foreach (var block in document)
        {
            if (block is ParagraphBlock para && para.Inline?.GetType() == typeof(ContainerInline))
            {
                bool paraSplits = false;

                foreach (var inline in para.Inline)
                {
                    if (inline is LineBreakInline lineBreak && lineBreak.IsHard)
                    {
                        SplitResult<T> splitResult;
                        if (currentLine.FirstChild != null &&
                            (splitResult = splitterFunction(currentLine.ToMarkdownString())).Split)
                        {
                            anySplits = true;
                            paraSplits = true;

                            // Return current document if needed
                            if (currentDoc.Count > 0 || previousLines.FirstChild != null)
                            {
                                if (previousLines.FirstChild != null)
                                    currentDoc.Add(new ParagraphBlock { Inline = previousLines });

                                yield return (currentDoc, currentMetadata);
                                currentDoc = new();
                                previousLines = new();
                                currentMetadata = default;
                            }

                            if (splitResult.Replacement != null)
                            {
                                currentLine.Clear();
                                currentLine.AppendChild(new LiteralInline(splitResult.Replacement));
                            }

                            if (splitResult.ConsumeFollowingContent)
                            {
                                foreach (var previousLine in currentLine)
                                    previousLines.AppendChild(previousLine);
                                previousLines.AppendChild(lineBreak);
                                currentLine.Clear();
                                currentMetadata = splitResult.Metadata;
                            }
                            else
                            {
                                currentDoc.Add(new ParagraphBlock { Inline = currentLine });
                                yield return (currentDoc, splitResult.Metadata);

                                currentDoc = new();
                                currentLine = new();
                                continue;
                            }
                        }
                        else
                        {
                            // TODO: dedupe with ConsumeFollowingContent block above
                            foreach (var previousLine in currentLine)
                                previousLines.AppendChild(previousLine);
                            previousLines.AppendChild(lineBreak);
                            currentLine.Clear();
                        }
                    }
                    else
                    {
                        currentLine.AppendChild(inline);
                    }
                }

                if (paraSplits)
                {
                    // TODO: dedupe with ConsumeFollowingContent block above
                    foreach (var previousLine in currentLine)
                        previousLines.AppendChild(previousLine);
                    currentLine.Clear();
                    currentDoc.Add(new ParagraphBlock { Inline = previousLines });
                    previousLines = new();
                }
                else
                {
                    // Add original paragraph if we didn't actually do anything
                    currentDoc.Add(para);
                    currentLine = new();
                    previousLines = new();
                }
            }
            else
            {
                currentDoc.Add(block);
            }
        }

        if (anySplits)
        {
            // Return current document if needed
            if (currentDoc.Count > 0)
                yield return (currentDoc, currentMetadata);
        }
        else
        {
            yield return (document, default);
        }
    }
}
