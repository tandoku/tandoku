namespace Tandoku.Subtitles.WebVtt;

public abstract class WebVttDocumentVisitor
{
    public virtual void VisitDocument(WebVttDocument document)
    {
        if (document.Regions is not null)
        {
            foreach (var region in document.Regions)
            {
                this.VisitRegion(region);
            }
        }

        if (document.Styles is not null)
        {
            foreach (var style in document.Styles)
            {
                this.VisitStyle(style);
            }
        }

        if (document.Cues is not null)
        {
            foreach (var cue in document.Cues)
            {
                this.VisitCue(cue);
            }
        }
    }

    public virtual void VisitRegion(RegionDefinition region)
    {
    }

    public virtual void VisitStyle(Style style)
    {
    }

    public virtual void VisitCue(Cue cue)
    {
        if (cue.Content is not null)
        {
            this.VisitSpans(cue.Content);
        }
    }

    public virtual void VisitSpans(IEnumerable<Span> spans)
    {
        foreach (var span in spans)
        {
            this.VisitSpan(span);
        }
    }

    public virtual void VisitSpan(Span span)
    {
        if (span.Children is not null)
        {
            this.VisitSpans(span.Children);
        }
    }
}
