namespace Tandoku;

using Markdig;
using Markdig.Syntax;

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
    // TODO: needed?
    internal static IEnumerable<(MarkdownDocument Markdown, T Metadata)> SplitConditionallyByLines<T>(
        string markdown,
        Func<string, SplitResult<T>> splitterFunction)
    {
        var doc = Markdown.Parse(markdown);
        return SplitConditionallyByLines(doc, splitterFunction);
    }

    internal static IEnumerable<(MarkdownDocument Markdown, T Metadata)> SplitConditionallyByLines<T>(
        MarkdownDocument document,
        Func<string, SplitResult<T>> splitterFunction)
    {
        // TODO: implement this
    }
}
