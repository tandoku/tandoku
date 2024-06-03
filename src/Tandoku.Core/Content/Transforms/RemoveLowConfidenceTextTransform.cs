namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.Text;

public sealed class RemoveLowConfidenceTextTransform(double confidenceThreshold) : ContentBlockRewriter
{
    private const char ReplacementChar = ' ';

    public override ContentBlock? Visit(TextBlock block)
    {
        if (!string.IsNullOrWhiteSpace(block.Text) &&
            block.Image?.Region?.Segments.Any(s => s.Confidence < confidenceThreshold) == true)
        {
            var textIndex = 0;
            var textBuilder = new StringBuilder(block.Text.Length);
            var newSegments = ImmutableList.CreateBuilder<ContentRegionSegment>();
            foreach (var segment in block.Image.Region.Segments)
            {
                // Retain whitespace/punctuation between segments in the text
                var nextIndex = block.Text.IndexOf(segment.Text, textIndex);
                if (nextIndex > textIndex)
                    textBuilder.Append(block.Text, textIndex, nextIndex - textIndex);
                textIndex = nextIndex + segment.Text.Length;

                if (segment.Confidence < confidenceThreshold)
                {
                    textBuilder.Append(ReplacementChar);
                }
                else
                {
                    textBuilder.Append(segment.Text);
                    newSegments.Add(segment);
                }
            }

            // TODO: what if there is other content on the block (like references)?
            return newSegments.Count > 0 ?
                block with
                {
                    Text = textBuilder.ToString().Trim(),
                    Image = block.Image with
                    {
                        Region = block.Image.Region with
                        {
                            Segments = newSegments.ToImmutable(),
                        }
                    }
                } :
                null;
        }
        return block;
    }
}
