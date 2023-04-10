using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Tandoku.CommandLine
{
    internal static class DictionaryCompiler
    {
        //private static DictionaryStore _store = LoadStore();

        internal static void Compile()
        {
            // Ensures index backwards compatibility
            var AppLuceneVersion = LuceneVersion.LUCENE_48;

            var indexLocation = @"C:\temp\jmdictindex";
            if (System.IO.Directory.Exists(indexLocation))
                System.IO.Directory.Delete(indexLocation, recursive: true);
            using var dir = FSDirectory.Open(indexLocation);

            //create an analyzer to process the text
            //using var analyzer = new StandardAnalyzer(AppLuceneVersion);
            using var analyzer = new KeywordAnalyzer();

            //create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            using var writer = new IndexWriter(dir, indexConfig);

            int count = 0;
            var glossType = new FieldType
            {
                IsStored = true,
            };
            foreach (var entry in DictionaryXmlReader.ReadEntries(@"C:\Data\OneDrive\Study & Practice\Japanese\JMdict\JMdict_e.xml"))
            {
                var doc = new Document();
                /*
                {
                    // StringField indexes but doesn't tokenize
                    new StringField("kanji", source.Name, Field.Store.YES),
                    new TextField("favoritePhrase", source.FavoritePhrase, Field.Store.YES)
                };*/

                foreach (var kanji in entry.Kanji)
                    doc.Add(new TextField("kanji", kanji, Field.Store.YES));

                foreach (var reading in entry.Readings)
                    doc.Add(new TextField("reading", reading, Field.Store.YES));

                foreach (var gloss in entry.Glosses)
                    doc.Add(new Field("gloss", gloss, glossType));

                writer.AddDocument(doc);
                count++;
            }
            writer.Flush(triggerMerge: false, applyAllDeletes: false);

            Console.WriteLine($"Wrote {count} entries to Lucene index");
        }

        //internal static void Run()
        //{
        //    string s;
        //    while (!string.IsNullOrEmpty(s = Console.ReadLine()))
        //    {
        //        if (_store.EntriesByReading.TryGetValue(s, out var entries))
        //        {
        //            foreach (var entry in entries)
        //            {
        //                Console.WriteLine($"Id: {entry.Id}");
        //                Console.WriteLine($"Readings: {string.Join(", ", entry.Readings)}");
        //                Console.WriteLine("Glosses:");
        //                foreach (var gloss in entry.Glosses)
        //                    Console.WriteLine($"    {gloss}");
        //                Console.WriteLine();
        //            }
        //        }
        //        else
        //        {
        //            Console.WriteLine($"Could not find '{s}'.");
        //        }
        //    }
        //}

        //internal static bool TryLookupKanji(string term, [NotNullWhen(true)] out List<DictionaryEntry>? entry)
        //{
        //    return _store.EntriesByKanji.TryGetValue(term, out entry);
        //}

        //internal static bool TryLookupReading(string term, [NotNullWhen(true)] out List<DictionaryEntry>? entry)
        //{
        //    return _store.EntriesByReading.TryGetValue(term, out entry);
        //}

        private static DictionaryStore LoadStore()
        {
            var stopwatch = new Stopwatch();
            Console.WriteLine("Loading JMdict...");
            stopwatch.Start();

            var store = DictionaryStore.Load(@"C:\Data\OneDrive\Study & Practice\Japanese\JMdict\JMdict_e.xml");

            stopwatch.Stop();
            Console.WriteLine($"Loaded JMdict in {stopwatch.Elapsed}.");
            Console.WriteLine($"Loaded {store.EntriesByReading.Count} unique readings");

            return store;
        }

        private sealed class DictionaryStore
        {
            private readonly IReadOnlyDictionary<string, List<DictionaryEntry>> entriesByKanji;
            private readonly IReadOnlyDictionary<string, List<DictionaryEntry>> entriesByReading;

            internal DictionaryStore(
                IReadOnlyDictionary<string, List<DictionaryEntry>> entriesByKanji,
                IReadOnlyDictionary<string, List<DictionaryEntry>> entriesByReading)
            {
                this.entriesByKanji = entriesByKanji;
                this.entriesByReading = entriesByReading;
            }

            internal IReadOnlyDictionary<string, List<DictionaryEntry>> EntriesByKanji => this.entriesByKanji;
            internal IReadOnlyDictionary<string, List<DictionaryEntry>> EntriesByReading => this.entriesByReading;

            public static DictionaryStore Load(string xmlPath)
            {
                var entriesByKanji = new Dictionary<string, List<DictionaryEntry>>();
                var entriesByReading = new Dictionary<string, List<DictionaryEntry>>();
                foreach (var entry in DictionaryXmlReader.ReadEntries(xmlPath))
                {
                    foreach (var kanji in entry.Kanji)
                    {
                        if (!entriesByKanji.TryGetValue(kanji, out var entryList))
                        {
                            entryList = new List<DictionaryEntry>(1);
                            entriesByKanji.Add(kanji, entryList);
                        }
                        entryList.Add(entry);
                    }

                    foreach (var reading in entry.Readings)
                    {
                        if (!entriesByReading.TryGetValue(reading, out var entryList))
                        {
                            entryList = new List<DictionaryEntry>(1);
                            entriesByReading.Add(reading, entryList);
                        }
                        entryList.Add(entry);
                    }
                }
                return new DictionaryStore(entriesByKanji, entriesByReading);
            }
        }

        internal sealed class DictionaryEntry
        {
            public DictionaryEntry(
                long id,
                IReadOnlyList<string> kanji,
                IReadOnlyList<string> readings,
                IReadOnlyList<string> glosses)
            {
                this.Id = id;
                this.Kanji = kanji;
                this.Readings = readings;
                this.Glosses = glosses;
            }

            public long Id { get; }
            public IReadOnlyList<string> Kanji { get; }
            public IReadOnlyList<string> Readings { get; }
            public IReadOnlyList<string> Glosses { get; }
        }

        private static class DictionaryXmlReader
        {
            internal static IEnumerable<DictionaryEntry> ReadEntries(string xmlPath)
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse,
                };

                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.ReadToFollowing("entry"))
                    {
                        var el = XElement.Load(reader.ReadSubtree());

                        var id = XmlConvert.ToInt64(el.Element("ent_seq").Value);

                        var kanji =
                            from k in el.Elements("k_ele")
                            select k.Element("keb").Value;

                        var readings =
                            from r in el.Elements("r_ele")
                            select r.Element("reb").Value;

                        var gloss =
                            from s in el.Elements("sense")
                            from g in s.Elements("gloss")
                            select g.Value;

                        yield return new DictionaryEntry(id, kanji.ToList(), readings.ToList(), gloss.ToList());
                    }
                }
            }
        }
    }
}
