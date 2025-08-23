namespace Tandoku.Subtitles.Ttml;

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

/// <summary>
/// Represents a Timed Text Markup Language 2 (TTML2) document.
/// </summary>
/// <remarks>See https://www.w3.org/TR/ttml2/ for details.</remarks>
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
    public List<XmlElement>? Any { get; set; }
}

public class TtmlStyling
{
    [XmlElement("style")]
    public List<TtmlStyle>? Styles { get; set; }
}

public enum TtmlRuby
{
    Container,
    Base,
    Text,
}

public class TtmlStyle
{
    [XmlAttribute("xml:id", Namespace = "http://www.w3.org/XML/1998/namespace")]
    public string? Id { get; set; }

    [XmlIgnore]
    public TtmlRuby? Ruby { get; set; }

    [XmlAttribute("ruby", Namespace = "http://www.w3.org/ns/ttml#styling")]
    public string? RubyString
    {
        get => this.Ruby?.ToString().ToLowerInvariant();
        set => this.Ruby = value is not null ? Enum.Parse<TtmlRuby>(value, ignoreCase: true) : null;
    }

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

public partial class TtmlParagraph
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

    /// <summary>
    /// Parses a TTML time expression.
    /// </summary>
    /// <remarks>See https://www.w3.org/TR/2018/REC-ttml1-20181108/#timing-value-timeExpression for details.</remarks>
    private static TimeSpan ToTimeSpan(string s)
    {
        // NOTE - Frames in clock time and offset time are not implemented and would require a different structure
        // (cannot convert to TimeSpan until we've parsed the TTML document and can identify the ttp:frameRate)
        // Also wallclock-time is not currently implemented

        var offsetTime = OffsetTimeRegex().Match(s);
        if (offsetTime.Success)
        {
            var timeCount = double.Parse(offsetTime.Groups[1].ValueSpan);
            var metric = offsetTime.Groups[2].ValueSpan;
            return metric switch
            {
                "h" => TimeSpan.FromHours(timeCount),
                "m" => TimeSpan.FromMinutes(timeCount),
                "s" => TimeSpan.FromSeconds(timeCount),
                "ms" => TimeSpan.FromMilliseconds(timeCount),
                "f" => throw new NotSupportedException($"Frames in time expression are not currently supported."),
                "t" => TimeSpan.FromTicks((long)timeCount),
                _ => throw new InvalidDataException($"Unexpected metric '{metric}'. This should not be possible."),
            };
        }
        return TimeSpan.Parse(s);
    }

    private static string ToString(TimeSpan timeSpan) => timeSpan.ToString(@"hh\:mm\:ss\.fff");

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)(h|m|s|ms|f|t)$")]
    private static partial Regex OffsetTimeRegex();
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
