namespace Tandoku.Content;

using System.IO.Abstractions;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Tandoku.Serialization;

public sealed class ContentIndexBuilder
{
    private readonly IFileSystem fileSystem;

    public ContentIndexBuilder(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        // TODO: add LuceneIndexFactory abstraction that wraps Directory implementation for unit testing
    }

    public async Task BuildAsync(string contentPath, string indexPath)
    {
        using var indexDir = FSDirectory.Open(indexPath);
        var analyzer = LuceneFactory.CreateAnalyzer();
        var indexConfig = LuceneFactory.CreateIndexWriterConfig(analyzer);
        using var writer = new IndexWriter(indexDir, indexConfig);

        var fields = new
        {
            Text = new TextField(ContentIndex.FieldNames.Text, string.Empty, Field.Store.YES),
            Id = new StringField(ContentIndex.FieldNames.Id, string.Empty, Field.Store.YES),
            File = new StringField(ContentIndex.FieldNames.File, string.Empty, Field.Store.YES),
            Block = new StringField(ContentIndex.FieldNames.Block, string.Empty, Field.Store.YES),
        };
        var doc = new Document
        {
            fields.Text,
            fields.Id,
            fields.File,
            fields.Block,
        };

        await foreach (var (block, contentFile) in this.GetAllContentBlocksAsync(contentPath))
        {
            // TODO require id?
            fields.Id.SetStringValue(block.Id ?? string.Empty);
            fields.File.SetStringValue(contentFile.Name);
            fields.Block.SetStringValue(block.ToJsonString());
            foreach (var chunk in block.Chunks)
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    fields.Text.SetStringValue(chunk.Text);
                    writer.AddDocument(doc);
                }
            }
        }
        writer.Flush(triggerMerge: false, applyAllDeletes: false);
    }

    private async IAsyncEnumerable<(ContentBlock Block, IFileInfo File)> GetAllContentBlocksAsync(string contentPath)
    {
        var contentDir = this.fileSystem.GetDirectory(contentPath);
        foreach (var contentFile in contentDir.EnumerateContentFiles())
        {
            await foreach (var block in YamlSerializer.ReadStreamAsync<ContentBlock>(contentFile))
                yield return (block, contentFile);
        }
    }
}
