namespace Tandoku.Content;

using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text.Json;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

public sealed class ContentIndexSearcher : IDisposable
{
    private Directory? indexDir;
    private IndexReader? indexReader;
    private IndexSearcher? searcher;
    private Analyzer? analyzer;

    public ContentIndexSearcher(string indexPath, IFileSystem? fileSystem = null)
    {
        this.indexDir = LuceneFactory.OpenDirectory(indexPath);
        this.indexReader = DirectoryReader.Open(this.indexDir);
        this.searcher = new IndexSearcher(this.indexReader);
        this.analyzer = LuceneFactory.CreateAnalyzer();
    }

    public void Dispose()
    {
        if (this.analyzer is not null)
        {
            this.analyzer.Dispose();
            this.analyzer = null;
        }
        this.searcher = null;
        if (this.indexReader is not null)
        {
            this.indexReader.Dispose();
            this.indexReader = null;
        }
        if (this.indexDir is not null)
        {
            this.indexDir.Dispose();
            this.indexDir = null;
        }
    }

    private Directory IndexDir => this.indexDir ?? throw CreateObjectDisposedException();
    private IndexReader IndexReader => this.indexReader ?? throw CreateObjectDisposedException();
    private IndexSearcher Searcher => this.searcher ?? throw CreateObjectDisposedException();
    private Analyzer Analyzer => this.analyzer ?? throw CreateObjectDisposedException();

    public IEnumerable<ContentBlock> FindBlocks(string searchText, int maxHits = 100)
    {
        var hits = this.FindBlockDocs(searchText, maxHits);

        if (hits is not null)
        {
            foreach (var scoreDoc in hits.ScoreDocs)
            {
                var block = this.LoadContentBlock(scoreDoc.Doc);
                yield return block;
            }
        }
    }

    public TopDocs? FindBlockDocs(string searchText, int maxHits = 100)
    {
        // TODO: stop using queryParser, just tokenize search text and construct all queries directly
        var queryParser = LuceneFactory.CreateQueryParser(ContentIndex.FieldNames.Text, this.Analyzer);

        // Try exact phrase match first...
        var query = queryParser.Parse(@$"""{searchText}""");
        var hits = this.Searcher.Search(query, maxHits);
        if (hits.TotalHits == 0)
        {
            // ... but fall back to token match if no hits
            var booleanQuery = new BooleanQuery();
            using (var tokenStream = this.Analyzer.GetTokenStream(ContentIndex.FieldNames.Text, searchText))
            {
                tokenStream.Reset();
                while (tokenStream.IncrementToken())
                {
                    var token = tokenStream.GetAttribute<ICharTermAttribute>().ToString();
                    var term = new Term(ContentIndex.FieldNames.Text, token);
                    booleanQuery.Add(new TermQuery(term), Occur.SHOULD);
                }
            }
            return this.Searcher.Search(booleanQuery, maxHits);
        }
        return null;
    }

    public ContentBlock LoadContentBlock(int docId)
    {
        var doc = this.Searcher.Doc(docId);
        var blockJson = doc.Get(ContentIndex.FieldNames.Block);
        var blockJsonDoc = JsonDocument.Parse(blockJson);
        return ContentBlock.Deserialize(blockJsonDoc) ??
            throw new InvalidDataException();
    }

    private static ObjectDisposedException CreateObjectDisposedException() =>
        new(nameof(ContentIndexSearcher));
}
