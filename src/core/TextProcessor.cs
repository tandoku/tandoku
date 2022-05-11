namespace Tandoku;

using Markdig;

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
            {
                // TODO: this changes offsets vs original markdown text, currently suppressing these from output
                // since they aren't needed yet anyway (and just add a lot of noise).
                var plainText = Markdown.ToPlainText(block.Text);
                block.Tokens.AddRange(tokenizer.Tokenize(plainText).Select(ScrubToken));
            }
            ProcessedBlocksCount++;
            yield return block;
        }
    }

    private static Token ScrubToken(Token token)
    {
        token.StartOffset = null;
        token.EndOffset = null;

        // Not sure what these are (they seem to always be 1), excluding for now
        token.PositionIncrement = null;
        token.PositionLength = null;

        return token;
    }
}
