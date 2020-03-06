using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace BlueMarsh.Tandoku.Reader
{
    [XmlRoot("document")]
    public class PdfDocument
    {
        [XmlElement("page")]
        public List<PdfPage>? Pages { get; set; }

        public static PdfDocument Load(string path)
        {
            var serializer = new XmlSerializer(typeof(PdfDocument));
            using (var stream = File.OpenRead(path))
                return (PdfDocument)serializer.Deserialize(stream);
        }
    }

    public class PdfPage
    {
        [XmlElement("block")]
        public List<PdfBlock>? Blocks { get; set; }

        public void WriteTo(TextWriter writer)
        {
            if (Blocks == null)
                return;

            foreach (var b in Blocks)
                b.WriteTo(writer);
        }
    }

    public class PdfBlock
    {
        [XmlElement("line")]
        public List<PdfLine>? Lines { get; set; }

        public void WriteTo(TextWriter writer)
        {
            if (Lines == null)
                return;

            foreach (var l in Lines)
            {
                l.WriteTo(writer);
                writer.WriteLine();
            }
        }
    }

    public class PdfLine
    {
        [XmlElement("font")]
        public List<PdfFont>? Fonts { get; set; }

        public void WriteTo(TextWriter writer)
        {
            if (Fonts == null)
                return;

            foreach (var f in Fonts)
            {
                f.WriteTo(writer);
            }
        }
    }

    public class PdfFont
    {
        [XmlElement("char")]
        public List<PdfChar>? Chars { get; set; }

        public void WriteTo(TextWriter writer)
        {
            if (Chars == null)
                return;

            foreach (var c in Chars)
            {
                c.WriteTo(writer);
            }
        }
    }

    public class PdfChar
    {
        [XmlAttribute("c")]
        public string? Char { get; set; }

        public void WriteTo(TextWriter writer)
        {
            writer.Write(Char);
        }
    }
}
