namespace Tandoku;

using System.Text.RegularExpressions;
using System.Xml.Linq;

public enum ContentTransformKind
{
    DetectSoundEffect,
    ExtractActor,
    ExtractParentheticalRubyText,
}

public sealed class TransformProcessor
{
    public IEnumerable<TextBlock> ApplyTransforms(
        IEnumerable<ContentTransformKind> transformKinds,
        IEnumerable<TextBlock> blocks)
    {
        var transforms = CreateContentTransforms(transformKinds).ToArray();

        var currentBlocks = new Queue<TextBlock>();
        var nextBlocks = new Queue<TextBlock>();

        foreach (var block in blocks)
        {
            currentBlocks.Enqueue(block);

            foreach (var transform in transforms)
            {
                while (currentBlocks.TryDequeue(out var curBlock))
                {
                    foreach (var nextBlock in transform.ApplyTransform(curBlock))
                        nextBlocks.Enqueue(nextBlock);
                }

                (currentBlocks, nextBlocks) = (nextBlocks, currentBlocks);
            }

            while (currentBlocks.TryDequeue(out var finalBlock))
                yield return finalBlock;
        }
    }

    private IEnumerable<ContentTransform> CreateContentTransforms(
        IEnumerable<ContentTransformKind> transformKinds)
    {
        foreach (var transformKind in transformKinds)
        {
            yield return transformKind switch
            {
                ContentTransformKind.DetectSoundEffect =>
                    new DetectSoundEffectTransform(),

                ContentTransformKind.ExtractActor =>
                    new ExtractActorTransform(),

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
        public abstract IEnumerable<TextBlock> ApplyTransform(TextBlock block);

        protected static Regex CreateRegex(
            string pattern,
            RegexOptions additionalOptions = RegexOptions.None)
        {
            var options =
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant |
                additionalOptions;

            return new Regex(pattern, options);
        }
    }

    private abstract class SimpleContentTransform : ContentTransform
    {
        public override IEnumerable<TextBlock> ApplyTransform(TextBlock block)
        {
            ApplyTransformCore(block);
            return new[] { block };
        }

        protected abstract void ApplyTransformCore(TextBlock block);
    }

    private sealed class DetectSoundEffectTransform : ContentTransform
    {
        private readonly IReadOnlyList<Regex> _regexes = new[]
        {
            CreateRegex(@"^(?:（.+?）|♪～|～♪)$"),
        };

        public override IEnumerable<TextBlock> ApplyTransform(TextBlock block)
        {
            if (block.ContentKind == ContentKind.Primary && !string.IsNullOrEmpty(block.Text))
            {
                foreach (var split in MarkdownSplitter.SplitConditionallyByLines(block.Text, SplitOnSoundEffect))
                {
                    if (split.Metadata != default || split.Markdown != block.Text)
                    {
                        var newBlock = block.Clone();
                        newBlock.Text = split.Markdown;
                        newBlock.ContentKind = split.Metadata;
                        yield return newBlock;
                    }
                    else
                    {
                        yield return block;
                    }
                }
            }
            else
            {
                yield return block;
            }
        }

        private SplitResult<ContentKind> SplitOnSoundEffect(string text)
        {
            foreach (var regex in _regexes)
            {
                if (regex.IsMatch(text))
                    return SplitResult.Create(true, metadata: ContentKind.SoundEffect);
            }
            return SplitResult.Create(false, metadata: default(ContentKind));
        }
    }

    private sealed class ExtractActorTransform : ContentTransform
    {
        private readonly IReadOnlyList<Regex> _regexes = new[]
        {
            CreateRegex(@"^（(.+?)）\s*(\S.*)$"),
        };

        public override IEnumerable<TextBlock> ApplyTransform(TextBlock block)
        {
            if (block.ContentKind == ContentKind.Primary && !string.IsNullOrEmpty(block.Text))
            {
                foreach (var split in MarkdownSplitter.SplitConditionallyByLines(block.Text, SplitOnActor))
                {
                    if (split.Metadata != null || split.Markdown != block.Text)
                    {
                        var newBlock = block.Clone();
                        newBlock.Text = split.Markdown;
                        newBlock.Actor = split.Metadata;
                        yield return newBlock;
                    }
                    else
                    {
                        yield return block;
                    }
                }
            }
            else
            {
                yield return block;
            }
        }

        private SplitResult<string> SplitOnActor(string text)
        {
            foreach (var regex in _regexes)
            {
                var match = regex.Match(text);
                if (match.Success)
                {
                    var actor = match.Groups[1].Value;
                    var remainingText = match.Groups[2].Value;
                    return SplitResult.Create(
                        true,
                        replacement: remainingText,
                        consumeFollowingContent: true,
                        metadata: actor);
                }
            }
            return SplitResult.Create(false, metadata: default(string));
        }
    }

    private sealed class ExtractParentheticalRubyTextTransform : SimpleContentTransform
    {
        private readonly IReadOnlyList<Regex> _parenRegexes = new[]
        {
            CreateRegex(@"(?<=\w)\((.+?)\)"),
            CreateRegex(@"(?<=\w)（(.+?)）"),
        };

        protected override void ApplyTransformCore(TextBlock block)
        {
            block.Text = TransformString(block.Text);
            block.Actor = TransformString(block.Actor);
        }

        private string? TransformString(string? text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var regex in _parenRegexes)
                {
                    text = regex.Replace(text, ReplaceWithRubyText);
                }
            }
            return text;
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
