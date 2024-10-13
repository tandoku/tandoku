namespace Tandoku.Content;

using System.Collections.Generic;
using System.IO.Abstractions;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

public sealed class ContentLinker(IFileSystem? fileSystem = null)
{
    public async Task<(int LinkedBlocks, int TotalBlocks)> LinkAsync(
        string inputPath,
        string outputPath,
        string indexPath,
        string linkName)
    {
        // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
        using var indexDir = FSDirectory.Open(indexPath);
        using var indexReader = DirectoryReader.Open(indexDir);
        var searcher = new IndexSearcher(indexReader);
        var analyzer = LuceneFactory.CreateAnalyzer();
        var queryParser = LuceneFactory.CreateQueryParser(ContentIndex.FieldNames.Text, analyzer);

        int matchedBlocks = 0;
        int unmatchedBlocks = 0;

        var transformer = new ContentTransformer(inputPath, outputPath, fileSystem);
        await transformer.TransformAsync(LinkChunk);

        return (matchedBlocks, matchedBlocks + unmatchedBlocks);

        ContentBlockChunk LinkChunk(ContentBlockChunk chunk)
        {
            if (!string.IsNullOrWhiteSpace(chunk.Text))
            {
                // Try exact phrase match first...
                var query = queryParser.Parse(@$"""{chunk.Text}""");
                var hits = searcher.Search(query, 1);
                if (hits.TotalHits == 0)
                {
                    // ... but fall back to token match if no hits
                    var booleanQuery = new BooleanQuery();
                    using (var tokenStream = analyzer.GetTokenStream(ContentIndex.FieldNames.Text, chunk.Text))
                    {
                        tokenStream.Reset();
                        while (tokenStream.IncrementToken())
                        {
                            var token = tokenStream.GetAttribute<ICharTermAttribute>().ToString();
                            booleanQuery.Add(new TermQuery(new Term(ContentIndex.FieldNames.Text, token)), Occur.SHOULD);
                        }
                    }
                    hits = searcher.Search(booleanQuery, 1);
                }

                if (hits.TotalHits > 0)
                {
                    var doc = searcher.Doc(hits.ScoreDocs[0].Doc);
                    var blockJson = doc.Get(ContentIndex.FieldNames.Block);
                    var linkedBlock = ContentBlock.DeserializeJson(blockJson) ??
                        throw new InvalidDataException();

                    matchedBlocks++;
                    return chunk with
                    {
                        // TODO add links (but keep copy to references as an option or separate command)
                        References = chunk.References.AddRange(
                            CopyLinkedBlockToReferences(linkedBlock, linkName))
                    };
                }
                else
                {
                    // TODO look into blocks with no matches
                    unmatchedBlocks++;
                }
            }
            return chunk;
        }
    }

    private static IEnumerable<KeyValuePair<string, Chunk>> CopyLinkedBlockToReferences(
        ContentBlock linkedBlock,
        string linkName)
    {
        var refs = new Dictionary<string, List<IMarkdownText>>();
        foreach (var chunk in linkedBlock.Chunks)
        {
            foreach (var (refKey, refText) in GetRefsForChunk(chunk))
            {
                if (!refs.TryGetValue(refKey, out var list))
                {
                    list = [];
                    refs.Add(refKey, list);
                }
                list.Add(refText);
            }
        }
        foreach (var reference in refs)
        {
            yield return KeyValuePair.Create(
                reference.Key,
                new Chunk
                {
                    Text = reference.Value.CombineText(MarkdownSeparator.Paragraph).Text
                });
        }

        IEnumerable<(string Key, IMarkdownText Text)> GetRefsForChunk(ContentBlockChunk chunk)
        {
            if (!string.IsNullOrEmpty(chunk.Text))
                yield return (linkName, chunk);

            foreach (var (refName, reference) in chunk.References)
            {
                if (!string.IsNullOrEmpty(reference.Text))
                    yield return ($"{linkName}-{refName}", reference);
            }
        }
    }
}
