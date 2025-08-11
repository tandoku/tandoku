namespace Tandoku.Subtitles.Ttml;

public abstract class TtmlDocumentVisitor
{
    public virtual void VisitDocument(TtmlDocument document)
    {
        if (document.Head is not null)
            this.VisitHead(document.Head);

        if (document.Body is not null)
            this.VisitBody(document.Body);
    }

    public virtual void VisitHead(TtmlHead head)
    {
        if (head.Metadata is not null)
            this.VisitMetadata(head.Metadata);

        if (head.Styling is not null)
            this.VisitStyling(head.Styling);
    }

    public virtual void VisitMetadata(TtmlMetadata metadata)
    {
    }

    public virtual void VisitStyling(TtmlStyling styling)
    {
        if (styling.Styles is not null)
        {
            foreach (var style in styling.Styles)
                this.VisitStyle(style);
        }
    }

    public virtual void VisitStyle(TtmlStyle style)
    {
    }

    public virtual void VisitBody(TtmlBody body)
    {
        if (body.Divs is not null)
        {
            foreach (var div in body.Divs)
                this.VisitDiv(div);
        }
    }

    public virtual void VisitDiv(TtmlDiv div)
    {
        if (div.Paragraphs is not null)
        {
            foreach (var paragraph in div.Paragraphs)
                this.VisitParagraph(paragraph);
        }
    }

    public virtual void VisitParagraph(TtmlParagraph paragraph)
    {
        if (paragraph.Content is not null)
        {
            foreach (var contentItem in paragraph.Content)
                this.VisitContentItem(contentItem);
        }
    }

    public virtual void VisitContentItem(object contentItem)
    {
        switch (contentItem)
        {
            case TtmlSpan span:
                this.VisitSpan(span);
                break;
            case TtmlBr br:
                this.VisitBr(br);
                break;
            case string text:
                this.VisitText(text);
                break;
            default:
                this.VisitUnknownContentItem(contentItem);
                break;
        }
    }

    public virtual void VisitSpan(TtmlSpan span)
    {
        if (span.Content is not null)
        {
            foreach (var contentItem in span.Content)
                this.VisitContentItem(contentItem);
        }
    }

    public virtual void VisitBr(TtmlBr br)
    {
    }

    public virtual void VisitText(string text)
    {
    }

    public virtual void VisitUnknownContentItem(object contentItem)
    {
        throw new InvalidDataException("Unknown content item type: " + contentItem.GetType().Name);
    }
}
