using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Analysis.Ja.TokenAttributes;
using Lucene.Net.Analysis.TokenAttributes;

namespace BlueMarsh.Tandoku
{
    public sealed class Tokenizer
    {
        private readonly JapaneseTokenizerFactory _tokenizerFactory;

        public Tokenizer()
        {
            _tokenizerFactory = new JapaneseTokenizerFactory(new Dictionary<string, string>());
        }

        public IEnumerable<Token> Tokenize(string text)
        {
            // TODO: optimize this to reuse tokenizer
            var tokenizer = _tokenizerFactory.Create(new StringReader(text));
            var termAttr = tokenizer.GetAttribute<ICharTermAttribute>();
            var offsetAttr = tokenizer.GetAttribute<IOffsetAttribute>();
            var posIncrAttr = tokenizer.GetAttribute<IPositionIncrementAttribute>();
            var posLenAttr = tokenizer.GetAttribute<IPositionLengthAttribute>();
            var baseFormAttr = tokenizer.GetAttribute<IBaseFormAttribute>();
            var inflectionAttr = tokenizer.GetAttribute<IInflectionAttribute>();
            var posAttr = tokenizer.GetAttribute<IPartOfSpeechAttribute>();
            var readingAttr = tokenizer.GetAttribute<IReadingAttribute>();
            tokenizer.Reset();
            long ordinal = 0;
            while (tokenizer.IncrementToken())
            {
                yield return new Token
                {
                    Ordinal = ++ordinal,
                    Term = termAttr.ToString(),
                    StartOffset = offsetAttr.StartOffset,
                    EndOffset = offsetAttr.EndOffset,
                    PositionIncrement = posIncrAttr.PositionIncrement,
                    PositionLength = posLenAttr.PositionLength,
                    BaseForm = baseFormAttr.GetBaseForm(),
                    PartOfSpeech = posAttr.GetPartOfSpeech(),
                    InflectionForm = inflectionAttr.GetInflectionForm(),
                    InflectionType = inflectionAttr.GetInflectionType(),
                    Pronunciation = readingAttr.GetPronunciation(),
                    Reading = readingAttr.GetReading(),
                };
            }
            tokenizer.End();
        }
    }
}
