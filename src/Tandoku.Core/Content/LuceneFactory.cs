namespace Tandoku.Content;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Store;
using Lucene.Net.Util;

internal static class LuceneFactory
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
    internal static Directory OpenDirectory(string path) => FSDirectory.Open(path);
    internal static Analyzer CreateAnalyzer() => new JapaneseAnalyzer(AppLuceneVersion); // TODO: set tokenizer mode?
    internal static IndexWriterConfig CreateIndexWriterConfig(Analyzer analyzer) => new(AppLuceneVersion, analyzer);

    internal static QueryParser CreateQueryParser(string field, Analyzer analyzer) => new(AppLuceneVersion, field, analyzer);
}
