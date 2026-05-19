namespace Tandoku.Markdown;

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
    public bool NoHeadings { get; init; }
    public bool KeepTogether { get; init; }
    public MarkdownRubyBehavior RubyBehavior { get; init; }
    public MarkdownReferenceBehavior ReferenceBehavior { get; init; }
    public MarkdownReferenceLabels ReferenceLabels { get; init; }
    public MarkdownQuirks Quirks { get; init; }
    public string? TemplatePath { get; init; }

    internal MarkdownRubyBehavior EffectiveRubyBehavior =>
        // KyBook 3 does not support ruby rendering — drop *Html ruby behaviors back to None
        this.Quirks == MarkdownQuirks.KyBook3 &&
        (this.RubyBehavior == MarkdownRubyBehavior.Html || this.RubyBehavior == MarkdownRubyBehavior.BlurHtml)
            ? MarkdownRubyBehavior.None
            : this.RubyBehavior;
}
