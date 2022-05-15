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
        LineBreakInline? previousLineBreak = null;
        T? metadata = default;

        bool anySplits = false;

        var doc = Markdown.Parse(markdown);
        var docBlocks = doc.ToArray();
        doc.Clear();

        foreach (var block in docBlocks)
        {
            if (block is ParagraphBlock para && para.Inline != null)
            {
                var originalInlines = para.Inline.ToArray();
                
                // para.Inline.Clear() reset parents but not siblings
                while (para.Inline.LastChild != null)
                    para.Inline.LastChild.Remove();

                foreach (var line in SplitIntoLines(originalInlines))
                {
                    var splitResult = splitterFunction(line.Inline.ToMarkdownString());
                    if (splitResult.Split)
                    {
                        anySplits = true;
                        previousLineBreak = null;

                        // Return current document if needed
                        if (doc.Count > 0 || para.Inline.FirstChild != null)
                        {
                            if (para.Inline.FirstChild != null)
                                doc.Add(para);

                            yield return (doc.ToMarkdownString(), metadata);
                            doc.Clear();
                            para.Inline.Clear();
                            metadata = default;
                        }

                        if (splitResult.Replacement != null)
                        {
                            line.Inline.Clear();
                            line.Inline.AppendChild(new LiteralInline(splitResult.Replacement));
                        }

                        if (splitResult.ConsumeFollowingContent)
                        {
                            foreach (var inline in line.Inline.ToArray()) //TODO...
                            {
                                inline.Remove();
                                para.Inline.AppendChild(inline);
                            }
                            previousLineBreak = line.LineBreak;
                            metadata = splitResult.Metadata;
                        }
                        else
                        {
                            foreach (var inline in line.Inline.ToArray()) //TODO...
                            {
                                inline.Remove();
                                para.Inline.AppendChild(inline);
                            }
                            doc.Add(para);
                            yield return (doc.ToMarkdownString(), splitResult.Metadata);

                            doc.Clear();
                            para.Inline.Clear();
                        }
                    }
                    else
                    {
                        if (previousLineBreak != null)
                        {
                            previousLineBreak.Remove();
                            para.Inline.AppendChild(previousLineBreak);
                            previousLineBreak = null;
                        }

                        // TODO: dedupe with ConsumeFollowingContent block above
                        foreach (var inline in line.Inline.ToArray()) //TODO...
                        {
                            inline.Remove();
                            para.Inline.AppendChild(inline);
                        }
                        previousLineBreak = line.LineBreak;
                    }
                }

                if (previousLineBreak != null)
                {
                    previousLineBreak.Remove();
                    para.Inline.AppendChild(previousLineBreak);
                    previousLineBreak = null;
                }

                if (para.Inline.FirstChild != null)
                    doc.Add(para);
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
                yield return (doc.ToMarkdownString(), metadata);
        }
        else
        {
            yield return (markdown, default);
        }
    }

    private static IEnumerable<(ContainerInline Inline, LineBreakInline? LineBreak)> SplitIntoLines(IEnumerable<Inline> inlines)
    {
        var currentLine = new ContainerInline();

        foreach (var inline in inlines)
        {
            if (inline is LineBreakInline lineBreak && lineBreak.IsHard)
            {
                yield return (currentLine, lineBreak);
                currentLine.Clear(); // TODO: clear or new?
            }
            else
            {
                currentLine.AppendChild(inline);
            }
        }

        if (currentLine.FirstChild != null)
            yield return (currentLine, null);
    }
}
