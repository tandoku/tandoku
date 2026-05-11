namespace Tandoku;

using Markdig;

public sealed class TextProcessor
{
    public long ProcessedBlocksCount { get; private set; }

    public void Tokenize(string path)
    {
        ApplyTransform(path, Tokenize);
    }

    public void Transform(string path, IEnumerable<ContentTransformKind> transforms)
    {
        var processor = new TransformProcessor();
        ApplyTransform(path, b => processor.ApplyTransforms(transforms, b));
    }

    private void ApplyTransform(
        string path,
        Func<IEnumerable<TextBlock>, IEnumerable<TextBlock>> transformBlockStream)
    {
        var serializer = new TextBlockSerializer();
        string tempPath = Path.GetTempFileName();
        var format = TextBlockFormatExtensions.FromFilePath(path);
        serializer.Serialize(
            tempPath,
            transformBlockStream(serializer.Deserialize(path)).Select(b => { ProcessedBlocksCount++; return b; }),
            format);
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

                // TODO: generalize warnings
                if (plainText.StartsWith('<') && (plainText.EndsWith('>') || plainText.EndsWith(">\n")))
                    Console.WriteLine($"Warning: block plain text appears to be XML: {plainText}");

                block.Tokens.AddRange(tokenizer.Tokenize(plainText).Select(ScrubToken));
            }
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
