namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.Text;

public sealed class RemoveLowConfidenceTextTransform(double confidenceThreshold) : ContentBlockTransform
{
    private const char ReplacementChar = ' ';

    protected override ContentBlockChunk? TransformChunk(ContentBlockChunk chunk)
    {
        if (!string.IsNullOrWhiteSpace(chunk.Text) &&
            chunk.Image?.TextSpans.Count > 0 &&
            chunk.Image?.TextSpans[0].Confidence < confidenceThreshold)
        {
            var textIndex = 0;
            var textBuilder = new StringBuilder(chunk.Text.Length);
            var newTextSpans = ImmutableList.CreateBuilder<ImageTextSpan>();
            foreach (var textSpan in chunk.Image.TextSpans)
            {
                // Retain whitespace/punctuation between spans in the text
                var nextIndex = chunk.Text.IndexOf(textSpan.Text, textIndex);
                if (nextIndex > textIndex)
                    textBuilder.Append(chunk.Text, textIndex, nextIndex - textIndex);
                textIndex = nextIndex + textSpan.Text.Length;

                if (textSpan.Confidence < confidenceThreshold)
                {
                    textBuilder.Append(ReplacementChar);
                }
                else
                {
                    textBuilder.Append(textSpan.Text);
                    newTextSpans.Add(textSpan);
                }
            }

            return newTextSpans.Count > 0 ?
                chunk with
                {
                    Text = textBuilder.ToString().Trim(),
                    Image = chunk.Image with
                    {
                        TextSpans = newTextSpans.ToImmutable(),
                    }
                } :
                null;
        }
        return chunk;
    }
}
