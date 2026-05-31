namespace Tandoku.Markdown;

using Scriban.Runtime;

public enum MarkdownRubyBehavior
{
    None,
    Html,
    BlurHtml,
    Remove,
}

public enum MarkdownReferenceBehavior
{
    None,
    Footnotes,
    BlurHtml,
}

public enum MarkdownReferenceLabels
{
    Default,
    All,
    None,
}

public enum MarkdownQuirks
{
    None,
    KyBook3,
}

public sealed record MarkdownExportSettings
{
    public bool Combine { get; init; }
    public bool NoBlockHeadings { get; init; }
    public MarkdownRubyBehavior RubyBehavior { get; init; }
    public MarkdownReferenceBehavior ReferenceBehavior { get; init; }
    public MarkdownReferenceLabels ReferenceLabels { get; init; }
    public MarkdownQuirks Quirks { get; init; }
    public string? TemplatePath { get; init; }

    // Arbitrary template-only options imported directly into the Scriban context.
    public ScriptObject CustomOptions { get; init; } = new();

    internal MarkdownExportSettings ApplyQuirks()
    {
        if (this.Quirks == MarkdownQuirks.KyBook3)
        {
            // KyBook 3 does not support ruby rendering — drop *Html ruby behaviors back to None
            var effectiveRubyBehavior =
                this.RubyBehavior is MarkdownRubyBehavior.Html or MarkdownRubyBehavior.BlurHtml ?
                MarkdownRubyBehavior.None :
                this.RubyBehavior;

            return this with
            {
                RubyBehavior = effectiveRubyBehavior,
            };
        }
        return this;
    }
}
