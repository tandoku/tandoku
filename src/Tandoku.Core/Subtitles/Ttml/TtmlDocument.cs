namespace Tandoku.Subtitles.Ttml;

using System.Xml.Serialization;

[XmlRoot("tt", Namespace = "http://www.w3.org/ns/ttml")]
public class TtmlDocument
{
    [XmlAttribute("xml:lang")]
    public string? Language { get; set; }

    [XmlElement("head")]
    public TtmlHead? Head { get; set; }

    [XmlElement("body")]
    public TtmlBody? Body { get; set; }
}

public class TtmlHead
{
    [XmlElement("metadata")]
    public TtmlMetadata? Metadata { get; set; }

    [XmlElement("styling")]
    public TtmlStyling? Styling { get; set; }
}

public class TtmlMetadata
{
    [XmlAnyElement]
    public List<System.Xml.XmlElement>? Any { get; set; }
}

public class TtmlStyling
{
    [XmlElement("style")]
    public List<TtmlStyle>? Styles { get; set; }
}

public class TtmlStyle
{
    [XmlAttribute("xml:id", Namespace = "http://www.w3.org/XML/1998/namespace")]
    public string? Id { get; set; }

    [XmlAttribute("ruby", Namespace = "http://www.w3.org/ns/ttml#styling")]
    public string? Ruby { get; set; }

    // Add other tts: attributes as needed
}

public class TtmlBody
{
    [XmlElement("div")]
    public List<TtmlDiv>? Divs { get; set; }
}

public class TtmlDiv
{
    [XmlElement("p")]
    public List<TtmlParagraph>? Paragraphs { get; set; }
}

public class TtmlParagraph
{
    [XmlIgnore]
    public TimeSpan Begin { get; set; }

    [XmlAttribute("begin")]
    public string BeginString
    {
        get => ToString(this.Begin);
        set => this.Begin = ToTimeSpan(value);
    }

    [XmlIgnore]
    public TimeSpan End { get; set; }

    [XmlAttribute("end")]
    public string EndString
    {
        get => ToString(this.End);
        set => this.End = ToTimeSpan(value);
    }

    // TODO - support duration attribute

    [XmlAttribute("style")]
    public string? Style { get; set; }

    [XmlElement("span", typeof(TtmlSpan))]
    [XmlElement("br", typeof(TtmlBr))]
    [XmlText(typeof(string))]
    public List<object>? Content { get; set; }

    // TODO - support other types of time expressions defined in TTML spec
    // https://www.w3.org/TR/2018/REC-ttml1-20181108/#timing-value-timeExpression
    private static TimeSpan ToTimeSpan(string s) => TimeSpan.Parse(s);
    private static string ToString(TimeSpan timeSpan) => timeSpan.ToString(@"hh\:mm\:ss\.fff");
}

public class TtmlSpan
{
    [XmlAttribute("style")]
    public string? Style { get; set; }

    [XmlElement("span", typeof(TtmlSpan))]
    [XmlElement("br", typeof(TtmlBr))]
    [XmlText(typeof(string))]
    public List<object>? Content { get; set; }
}

public class TtmlBr
{
    // <br/> is an empty element, so no properties are needed.
}
