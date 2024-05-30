namespace Tandoku.Content;

using System.IO.Abstractions;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Tandoku.Serialization;

public sealed class ContentIndexBuilder
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    private readonly IFileSystem fileSystem;

    public ContentIndexBuilder(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
    }

    public async Task BuildAsync(string contentPath, string indexPath)
    {
        using var indexDir = FSDirectory.Open(indexPath);
        var analyzer = new JapaneseAnalyzer(AppLuceneVersion); // TODO: set tokenizer mode?
        var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
        using var writer = new IndexWriter(indexDir, indexConfig);

        var textField = new TextField("text", string.Empty, Field.Store.YES);
        var idField = new StringField("id", string.Empty, Field.Store.YES);
        var contentField = new StringField("content", string.Empty, Field.Store.YES);
        var doc = new Document
        {
            textField,
            idField,
            contentField,
        };

        await foreach (var (block, contentFile) in this.GetAllContentBlocksAsync(contentPath))
        {
            // TODO require id?
            idField.SetStringValue(block.Id ?? string.Empty);
            contentField.SetStringValue(contentFile.Name);
            switch (block)
            {
                case TextBlock textBlock:
                    if (!string.IsNullOrEmpty(textBlock.Text))
                    {
                        textField.SetStringValue(textBlock.Text);
                        writer.AddDocument(doc);
                    }
                    break;

                case CompositeBlock compositeBlock:
                    foreach (var nestedBlock in compositeBlock.Blocks)
                    {
                        if (!string.IsNullOrEmpty(nestedBlock.Text))
                        {
                            textField.SetStringValue(nestedBlock.Text);
                            writer.AddDocument(doc);
                        }
                    }
                    break;

                default:
                    throw new InvalidDataException();
            }
        }
        writer.Flush(triggerMerge: false, applyAllDeletes: false);
    }

    private async IAsyncEnumerable<(ContentBlock, IFileInfo)> GetAllContentBlocksAsync(string contentPath)
    {
        var contentDir = this.fileSystem.GetDirectory(contentPath);
        foreach (var contentFile in contentDir.EnumerateFiles("*.content.yaml"))
        {
            await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(contentFile))
                yield return (block, contentFile);
        }
    }
}
