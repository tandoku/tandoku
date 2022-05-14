namespace Tandoku;

using Markdig;
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

internal static class MarkdownSplitter
{
    internal static IEnumerable<(string Markdown, T? Metadata)> SplitConditionallyByLines<T>(
        string markdown,
        Func<string, SplitResult<T>> splitterFunction)
    {
        var currentLine = new ContainerInline();
        var previousLines = new ContainerInline();
        LineBreakInline? previousLineBreak = null;
        T? currentMetadata = default;

        bool anySplits = false;

        var doc = Markdown.Parse(markdown);
        var docBlocks = doc.ToArray();
        doc.Clear();

        foreach (var block in docBlocks)
        {
            if (block is ParagraphBlock para && para.Inline?.GetType() == typeof(ContainerInline))
            {
                bool paraSplits = false;

                foreach (var inline in para.Inline.ToArray()) // TODO: queue inlines, split on final line within loop
                {
                    if (inline is LineBreakInline lineBreak && lineBreak.IsHard)
                    {
                        SplitResult<T> splitResult;
                        if (currentLine.FirstChild != null &&
                            (splitResult = splitterFunction(currentLine.ToMarkdownString())).Split)
                        {
                            anySplits = true;
                            paraSplits = true;
                            previousLineBreak = null;

                            // Return current document if needed
                            if (doc.Count > 0 || previousLines.FirstChild != null)
                            {
                                if (previousLines.FirstChild != null)
                                    doc.Add(new ParagraphBlock { Inline = previousLines });

                                yield return (doc.ToMarkdownString(), currentMetadata);
                                doc.Clear();
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
                                foreach (var previousLine in currentLine.ToArray()) //TODO...
                                {
                                    previousLine.Remove();
                                    previousLines.AppendChild(previousLine);
                                }
                                previousLineBreak = lineBreak;
                                currentLine.Clear();
                                currentMetadata = splitResult.Metadata;
                            }
                            else
                            {
                                doc.Add(new ParagraphBlock { Inline = currentLine });
                                yield return (doc.ToMarkdownString(), splitResult.Metadata);

                                doc.Clear();
                                currentLine = new();
                                continue;
                            }
                        }
                        else
                        {
                            // TODO: dedupe with ConsumeFollowingContent block above
                            foreach (var previousLine in currentLine.ToArray())
                            {
                                previousLine.Remove();
                                previousLines.AppendChild(previousLine);
                            }
                            previousLineBreak = lineBreak;
                            currentLine.Clear();
                        }
                    }
                    else
                    {
                        if (previousLineBreak != null)
                        {
                            previousLineBreak.Remove();
                            currentLine.AppendChild(previousLineBreak);
                            previousLineBreak = null;
                        }

                        inline.Remove();
                        currentLine.AppendChild(inline);
                    }
                }

                if (paraSplits)
                {
                    // TODO: dedupe with blocks above
                    if (previousLineBreak != null)
                    {
                        previousLineBreak.Remove();
                        currentLine.AppendChild(previousLineBreak);
                        previousLineBreak = null;
                    }
                    foreach (var previousLine in currentLine.ToArray())
                    {
                        previousLine.Remove();
                        previousLines.AppendChild(previousLine);
                    }
                    currentLine.Clear();
                    doc.Add(new ParagraphBlock { Inline = previousLines });
                    previousLines = new();
                }
                else
                {
                    // Add original paragraph if we didn't actually do anything
                    doc.Add(para);
                    currentLine = new();
                    previousLines = new();
                    previousLineBreak = null;
                }
            }
            else
            {
                doc.Add(block);
            }
        }

        if (anySplits)
        {
            // Return current document if needed
            if (doc.Count > 0)
                yield return (doc.ToMarkdownString(), currentMetadata);
        }
        else
        {
            yield return (markdown, default);
        }
    }
}
