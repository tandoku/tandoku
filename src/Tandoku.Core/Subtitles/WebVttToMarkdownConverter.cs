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
        internal StringBuilder StringBuilder { get; } = new();

        public override void VisitSpan(Span span)
        {
            switch (span.Type)
            {
                case SpanType.Ruby:
                    this.StringBuilder.Append(' ');
                    base.VisitSpan(span);
                    return;

                case SpanType.RubyText:
                    this.StringBuilder.Append('[');
                    base.VisitSpan(span);
                    this.StringBuilder.Append(']');
                    return;

                case SpanType.Text:
                    this.StringBuilder.Append(span.Text);
                    return;

                case SpanType.LineTerminator:
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
