namespace Tandoku.Content;

using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text.Json;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

public sealed class ContentLinker(IFileSystem? fileSystem = null)
{
    private static readonly string DoubleNewLine = $"{Environment.NewLine}{Environment.NewLine}";

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
        await transformer.TransformAsync(LinkTextBlock);

        return (matchedBlocks, matchedBlocks + unmatchedBlocks);

        TextBlock LinkTextBlock(TextBlock textBlock)
        {
            if (!string.IsNullOrWhiteSpace(textBlock.Text))
            {
                // Try exact phrase match first...
                var query = queryParser.Parse(@$"""{textBlock.Text}""");
                var hits = searcher.Search(query, 1);
                if (hits.TotalHits == 0)
                {
                    // ... but fall back to token match if no hits
                    var booleanQuery = new BooleanQuery();
                    using (var tokenStream = analyzer.GetTokenStream(ContentIndex.FieldNames.Text, textBlock.Text))
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
                    var blockJsonDoc = JsonDocument.Parse(blockJson);
                    var linkedBlock = ContentBlock.Deserialize(blockJsonDoc) ??
                        throw new InvalidDataException();

                    matchedBlocks++;
                    return textBlock with
                    {
                        // TODO add links (but keep copy to references as an option or separate command)
                        References = textBlock.References.AddRange(
                            CopyLinkedBlockToReferences(linkedBlock, linkName))
                    };
                }
                else
                {
                    // TODO look into blocks with no matches
                    unmatchedBlocks++;
                }
            }
            return textBlock;
        }
    }

    private static IEnumerable<KeyValuePair<string, ContentReference>> CopyLinkedBlockToReferences(ContentBlock linkedBlock, string linkName)
    {
        switch (linkedBlock)
        {
            case TextBlock textBlock:
                foreach (var reference in GetRefsForTextBlock(textBlock))
                {
                    yield return KeyValuePair.Create(
                        reference.Key,
                        new ContentReference { Text = reference.Value });
                }
                break;

            case CompositeBlock compositeBlock:
                var refs = new Dictionary<string, List<string>>();
                foreach (var nestedBlock in compositeBlock.Blocks)
                {
                    foreach (var reference in GetRefsForTextBlock(nestedBlock))
                    {
                        if (!refs.TryGetValue(reference.Key, out var list))
                        {
                            list = [];
                            refs.Add(reference.Key, list);
                        }
                        list.Add(reference.Value);
                    }
                }
                foreach (var reference in refs)
                {
                    yield return KeyValuePair.Create(
                        reference.Key,
                        new ContentReference { Text = string.Join(DoubleNewLine, reference.Value) });
                }
                break;

            default:
                throw new InvalidDataException();
        }

        IEnumerable<KeyValuePair<string, string>> GetRefsForTextBlock(TextBlock textBlock)
        {
            if (!string.IsNullOrEmpty(textBlock.Text))
                yield return KeyValuePair.Create(linkName, textBlock.Text);

            foreach (var (refName, reference) in textBlock.References)
            {
                if (!string.IsNullOrEmpty(reference.Text))
                    yield return KeyValuePair.Create($"{linkName}-{refName}", reference.Text);
            }
        }
    }
}
