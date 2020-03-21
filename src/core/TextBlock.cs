using System;
using System.Collections.Generic;
using System.Text;

namespace BlueMarsh.Tandoku
{
    public sealed class TextBlock
    {
        public string? Text { get; set; }
        public List<Token> Tokens { get; } = new List<Token>();
        public string? Location { get; set; }
    }

    public sealed class Token
    {
        public long? Ordinal { get; set; }
        public string? Term { get; set; }
        public int? StartOffset { get; set; }
        public int? EndOffset { get; set; }
        public int? PositionIncrement { get; set; }
        public int? PositionLength { get; set; }
        public string? BaseForm { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? InflectionForm { get; set; }
        public string? InflectionType { get; set; }
        public string? Pronunciation { get; set; }
        public string? Reading { get; set; }
    }
}
