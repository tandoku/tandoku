namespace Tandoku.Subtitles;

using System.Text;
using Tandoku.Subtitles.WebVtt;

public static class WebVttToMarkdownConverter
{
    public static string Convert(IEnumerable<Span> spans)
    {
        var converter = new ConversionVisitor();
        converter.VisitSpans(spans);
        return converter.StringBuilder.ToString();
    }

    private sealed class ConversionVisitor : WebVttDocumentVisitor
    {
        private int rubyContext = 0;

        internal StringBuilder StringBuilder { get; } = new();

        public override void VisitSpan(Span span)
        {
            switch (span.Type)
            {
                case SpanType.Ruby:
                    // Add space before ruby if necessary to avoid ambiguity.
                    // This behavior needs to match the pattern used by tandoku markdown export to handle ruby.
                    if (this.StringBuilder.Length > 0 && this.StringBuilder[^1].IsRegexWordCharacter())
                        this.StringBuilder.Append(' ');
                    this.rubyContext++;
                    base.VisitSpan(span);
                    this.rubyContext--;
                    return;

                case SpanType.RubyText:
                    this.StringBuilder.Append('[');
                    this.rubyContext++;
                    base.VisitSpan(span);
                    this.rubyContext--;
                    this.StringBuilder.Append(']');
                    return;

                case SpanType.Text:
                    if (this.rubyContext > 0)
                    {
                        // TODO - log warning when ruby text contains spaces instead of silently dropping them
                        var text = span.Text.Replace(" ", string.Empty);
                        if (text.Any(c => !c.IsRegexWordCharacter()))
                            throw new InvalidDataException($"Ruby text '{span.Text}' contains one or more non-word characters. Only word characters are allowed in ruby text.");
                        this.StringBuilder.Append(text);
                    }
                    else
                    {
                        this.StringBuilder.Append(span.Text);
                    }
                    return;

                case SpanType.LineTerminator:
                    if (this.rubyContext > 0)
                        throw new InvalidDataException("Invalid line terminiator within ruby text.");
                    this.StringBuilder.Append("  ");
                    this.StringBuilder.AppendLine();
                    return;

                default:
                    base.VisitSpan(span);
                    return;
            }
        }
    }
}
