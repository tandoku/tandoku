namespace Tandoku;

public sealed class TextProcessor
{
    public long ProcessedBlocksCount { get; private set; }

    public void Tokenize(string path)
    {
        var serializer = new TextBlockSerializer();
        string tempPath = Path.GetTempFileName();
        var format = TextBlockFormatExtensions.FromFilePath(path);
        serializer.Serialize(tempPath, Tokenize(serializer.Deserialize(path)), format);
        File.Delete(path);
        File.Move(tempPath, path);
    }

    private IEnumerable<TextBlock> Tokenize(IEnumerable<TextBlock> blocks)
    {
        var tokenizer = new Tokenizer();
        foreach (var block in blocks)
        {
            block.Tokens.Clear();
            if (block.Text != null)
                block.Tokens.AddRange(tokenizer.Tokenize(block.Text));
            ProcessedBlocksCount++;
            yield return block;
        }
    }
}
