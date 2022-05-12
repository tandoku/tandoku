using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tandoku;

public enum ContentTransformKind
{
    ExtractParentheticalRubyText,
}

public sealed class TransformProcessor
{
    public IEnumerable<TextBlock> ApplyTransforms(
        IEnumerable<ContentTransformKind> transformKinds,
        IEnumerable<TextBlock> blocks)
    {
        var transforms = CreateContentTransforms(transformKinds).ToArray();

        foreach (var block in blocks)
        {
            foreach (var transform in transforms)
                transform.ApplyTransform(block);

            yield return block;
        }
    }

    private IEnumerable<ContentTransform> CreateContentTransforms(
        IEnumerable<ContentTransformKind> transformKinds)
    {
        foreach (var transformKind in transformKinds)
        {
            yield return transformKind switch
            {
                ContentTransformKind.ExtractParentheticalRubyText =>
                    new ExtractParentheticalRubyTextTransform(),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(transformKinds),
                        transformKind,
                        $"Unexpected transform kind: {transformKind}"),
            };
        }
    }

    private abstract class ContentTransform
    {
        public abstract void ApplyTransform(TextBlock block);
    }

    // TODO: add transforms to detect sound effects (set ContentKind), extract Actor

    private sealed class ExtractParentheticalRubyTextTransform : ContentTransform
    {
        private readonly IReadOnlyList<Regex> _parenRegexes = new Regex[]
        {
            new Regex(@"\((.+?)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(@"（(.+?)）", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        };

        public override void ApplyTransform(TextBlock block)
        {
            if (string.IsNullOrEmpty(block.Text))
                return;

            string text = block.Text;

            foreach (var regex in _parenRegexes)
            {
                text = regex.Replace(text, ReplaceWithRubyText);
            }

            block.Text = text;
        }

        private static string ReplaceWithRubyText(Match match)
        {
            var text = match.Groups[1].Value;
            var rubyElem = new XElement("ruby");
            rubyElem.SetAttributeValue("text", text);
            return rubyElem.ToString();
        }
    }
}
