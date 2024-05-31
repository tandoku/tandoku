namespace Tandoku.Content;

using System.IO.Abstractions;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Tandoku.Serialization;
using System.Text.Json;
using System.Collections.Generic;

public sealed class ContentLinker
{
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

        // TODO restructure this as common content transform
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
                        var query = queryParser.Parse(@$"""{textBlock.Text}""");
                        var hits = searcher.Search(query, 1);
                        if (hits.TotalHits > 0)
                        {
                            var doc = indexReader.Document(hits.ScoreDocs[0].Doc);
                            var blockJson = doc.GetField(ContentIndex.FieldNames.Block).GetStringValue();
                            var blockJsonDoc = JsonDocument.Parse(blockJson);
                            var linkedBlock = ContentBlock.Deserialize(blockJsonDoc) ??
                                throw new InvalidDataException();

                            textBlock = textBlock with
                            {
                                References = textBlock.References.AddRange(
                                    CopyLinkedBlockToReferences(linkedBlock, linkName))
                            };
                        }
                        yield return textBlock;
                        break;

                    case CompositeBlock compositeBlock:
                        // TODO
                        yield return compositeBlock;
                        break;

                    default:
                        throw new InvalidDataException();
                }
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, ContentReference>> CopyLinkedBlockToReferences(ContentBlock linkedBlock, string linkName)
    {
        switch (linkedBlock)
        {
            case TextBlock textBlock:
                if (!string.IsNullOrEmpty(textBlock.Text))
                {
                    yield return KeyValuePair.Create(linkName, new ContentReference { Text = textBlock.Text });
                }
                foreach (var (refName, reference) in textBlock.References)
                {
                    if (!string.IsNullOrEmpty(reference.Text))
                    {
                        yield return KeyValuePair.Create($"{linkName}-{refName}", reference);
                    }
                }
                break;

            case CompositeBlock compositeBlock:
                // TODO
                break;

            default:
                throw new InvalidDataException();
        }
    }
}

