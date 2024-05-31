namespace Tandoku.Content;

using System.IO.Abstractions;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Tandoku.Serialization;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Immutable;

public sealed class ContentLinker
{
    private static readonly string DoubleNewLine = $"{Environment.NewLine}{Environment.NewLine}";

    private readonly IFileSystem fileSystem;

    public ContentLinker(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
    }

    public async Task LinkAsync(string inputPath, string outputPath, string indexPath, string linkName)
    {
        using var indexDir = FSDirectory.Open(indexPath);
        using var indexReader = DirectoryReader.Open(indexDir);
        var searcher = new IndexSearcher(indexReader);
        var analyzer = LuceneFactory.CreateAnalyzer();
        var queryParser = LuceneFactory.CreateQueryParser(ContentIndex.FieldNames.Text, analyzer);

        // TODO restructure this as common content transform?
        var inputDir = this.fileSystem.GetDirectory(inputPath);
        var outputDir = this.fileSystem.GetDirectory(outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateFiles("*.content.yaml")) // TODO share with ContentIndexBuilder
        {
            var outputFile = outputDir.GetFile(inputFile.Name);
            await YamlSerializer.WriteStreamAsync(outputFile, LinkBlocks(inputFile));
        }

        async IAsyncEnumerable<ContentBlock> LinkBlocks(IFileInfo inputFile)
        {
            await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(inputFile))
            {
                switch (block)
                {
                    case TextBlock textBlock:
                        yield return LinkTextBlock(textBlock);
                        break;

                    case CompositeBlock compositeBlock:
                        yield return compositeBlock with
                        {
                            Blocks = compositeBlock.Blocks.Select(LinkTextBlock).ToImmutableList(),
                        };
                        break;

                    default:
                        throw new InvalidDataException();
                }
            }
        }

        TextBlock LinkTextBlock(TextBlock textBlock)
        {
            var query = queryParser.Parse(@$"""{textBlock.Text}""");
            var hits = searcher.Search(query, 1);
            if (hits.TotalHits > 0)
            {
                var doc = searcher.Doc(hits.ScoreDocs[0].Doc);
                var blockJson = doc.Get(ContentIndex.FieldNames.Block);
                var blockJsonDoc = JsonDocument.Parse(blockJson);
                var linkedBlock = ContentBlock.Deserialize(blockJsonDoc) ??
                    throw new InvalidDataException();

                textBlock = textBlock with
                {
                    References = textBlock.References.AddRange(
                        CopyLinkedBlockToReferences(linkedBlock, linkName))
                };
            }
            else
            {
                // TODO look into blocks with no matches
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

