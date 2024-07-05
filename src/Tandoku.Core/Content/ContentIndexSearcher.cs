namespace Tandoku.Content;

using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text.Json;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

public sealed class ContentIndexSearcher(IFileSystem? fileSystem = null)
{
    public async IAsyncEnumerable<ContentBlock> FindBlocksAsync(
        string searchQuery,
        string indexPath,
        int maxHits = 100)
    {
        // TODO: refactor to share this with ContentLinker

        // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
        using var indexDir = FSDirectory.Open(indexPath);
        using var indexReader = DirectoryReader.Open(indexDir);
        var searcher = new IndexSearcher(indexReader);
        var analyzer = LuceneFactory.CreateAnalyzer();
        var queryParser = LuceneFactory.CreateQueryParser(ContentIndex.FieldNames.Text, analyzer);

        // Try exact phrase match first...
        var query = queryParser.Parse(@$"""{searchQuery}""");
        var hits = searcher.Search(query, maxHits);
        if (hits.TotalHits == 0)
        {
            // ... but fall back to token match if no hits
            var booleanQuery = new BooleanQuery();
            using (var tokenStream = analyzer.GetTokenStream(ContentIndex.FieldNames.Text, searchQuery))
            {
                tokenStream.Reset();
                while (tokenStream.IncrementToken())
                {
                    var token = tokenStream.GetAttribute<ICharTermAttribute>().ToString();
                    booleanQuery.Add(new TermQuery(new Term(ContentIndex.FieldNames.Text, token)), Occur.SHOULD);
                }
            }
            hits = searcher.Search(booleanQuery, maxHits);
        }

        foreach (var scoreDoc in hits.ScoreDocs)
        {
            var doc = searcher.Doc(scoreDoc.Doc);
            var blockJson = doc.Get(ContentIndex.FieldNames.Block);
            var blockJsonDoc = JsonDocument.Parse(blockJson);
            var block = ContentBlock.Deserialize(blockJsonDoc) ??
                throw new InvalidDataException();
            yield return block;
        }
    }
}
