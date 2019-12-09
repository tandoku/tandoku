using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Analysis.Ja.TokenAttributes;
using Lucene.Net.Analysis.TokenAttributes;

namespace BlueMarsh.Tandoku.CommandLine
{
    internal static class TokenizerDemo
    {
        internal static void Dump()
        {
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("tandoku: expecting input");
                return;
            }

            using (new Utf8EncodingOverride())
            {
                // Ignore input encoding from console, use UTF-8 by default
                using var r = new StreamReader(Console.OpenStandardInput());

                // TODO: try out different modes (normal vs search)

                var factory = new JapaneseTokenizerFactory(new Dictionary<string, string>());
                var tokenizer = factory.Create(r);
                int i = 0;
                var termAttr = tokenizer.GetAttribute<ICharTermAttribute>();
                var offsetAttr = tokenizer.GetAttribute<IOffsetAttribute>();
                var posIncrAttr = tokenizer.GetAttribute<IPositionIncrementAttribute>();
                var posLenAttr = tokenizer.GetAttribute<IPositionLengthAttribute>();
                var baseFormAttr = tokenizer.GetAttribute<IBaseFormAttribute>();
                var inflectionAttr = tokenizer.GetAttribute<IInflectionAttribute>();
                var posAttr = tokenizer.GetAttribute<IPartOfSpeechAttribute>();
                var readingAttr = tokenizer.GetAttribute<IReadingAttribute>();
                tokenizer.Reset();
                Console.Write($"{"Num",-3}  {"Term",-10}");
                Console.Write($" {"St",-2}/{"En",-2}");
                Console.Write($" {"Ic",-2} {"Ln",-2}");
                Console.Write($" {"Base",-10}");
                Console.Write($" {"POS",-18}");
                Console.Write($" {"InflForm",-8} / {"InflType",-16}");
                Console.Write($" {"Pronunctn",-10} / {"Reading",-10}");
                Console.WriteLine();
                while (tokenizer.IncrementToken())
                {
                    Console.Write($"{i++,3}: {Align(termAttr, 10)}");
                    Console.Write($" {offsetAttr.StartOffset,2}/{offsetAttr.EndOffset,2}");
                    Console.Write($" {posIncrAttr.PositionIncrement,2} {posLenAttr.PositionLength,2}");
                    Console.Write($" {Align(baseFormAttr.GetBaseForm(), 10)}");
                    Console.Write($" {Align(posAttr.GetPartOfSpeech(), 18)}");
                    Console.Write($" {Align(inflectionAttr.GetInflectionForm(), 8)} / {Align(inflectionAttr.GetInflectionType(), 16)}");
                    Console.Write($" {Align(readingAttr.GetPronunciation(), 10)} / {Align(readingAttr.GetReading(), 10)}");
                    Console.WriteLine();

                    if (DictionaryLookupDemo.TryLookupKanji(termAttr.ToString(), out var entries) ||
                        DictionaryLookupDemo.TryLookupReading(termAttr.ToString(), out entries))
                    {
                        foreach (var entry in entries)
                        {
                            Console.WriteLine($"    {string.Join("; ", entry.Glosses)}");
                        }
                    }
                }
                tokenizer.End();
            }
        }

        private static string Align(object o, int width)
        {
            string s = o?.ToString() ?? string.Empty;

            // TODO: count number of CJK chars instead of assuming
            int wideChars = s.Count(c => char.IsLetter(c));
            int adjustedLen = (wideChars * 2) + (s.Length - wideChars);
            return s + new string(' ', Math.Max(0, width - adjustedLen));
        }

        // TODO: move to BlueMarsh.Common
        private sealed class Utf8EncodingOverride : IDisposable
        {
            private readonly Encoding? originalEncoding;

            public Utf8EncodingOverride()
            {
                if (!(Console.OutputEncoding is UTF8Encoding))
                {
                    this.originalEncoding = Console.OutputEncoding;
                    Console.OutputEncoding = new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: false);
                }
            }

            public void Dispose()
            {
                if (this.originalEncoding != null)
                    Console.OutputEncoding = this.originalEncoding;
            }
        }
    }
}
