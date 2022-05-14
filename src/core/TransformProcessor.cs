namespace Tandoku;

using System.Diagnostics.CodeAnalysis;
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

        /*
        protected static IEnumerable<TextLineInfo> GetTextLines(string markdown)
        {
            var doc = Markdown.Parse(markdown);
            var inlines = new ContainerInline();

            foreach (var block in doc)
            {
                if (block is ParagraphBlock para && para.Inline != null)
                {
                    foreach (var inline in para.Inline)
                    {
                        if (inline is LineBreakInline lineBreak && lineBreak.IsHard)
                        {
                            yield return new(inlines.ToMarkdownString(), null, lineBreak);
                            inlines.Clear();
                        }
                        else
                        {
                            inlines.AppendChild(inline);
                        }
                    }

                    yield return new(inlines.ToMarkdownString(), null, null);
                    inlines.Clear();
                }
                else
                {
                    yield return new(null, block, null);
                }
            }
        }

        protected record TextLineInfo(string? Text, MarkdownObject? MarkdownObj, LineBreakInline? LineBreak);*/
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

        /*protected override void ApplyTransformCore(TextBlock block)
        {
            var text = block.Text;
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var regex in _regexes)
            {
                if (regex.IsMatch(text))
                {
                    block.ContentKind = ContentKind.SoundEffect;
                    break;
                }
            }
        }*/

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

        /*public override IEnumerable<TextBlock> ApplyTransform(TextBlock block)
        {
            if (block.ContentKind != ContentKind.Primary || string.IsNullOrEmpty(block.Text))
            {
                yield return block;
            }
            else
            {
                var lines = GetLines(block.Text).ToList();
                if (lines.Any(l => l.IsMatch))
                {
                    var newBlock = block.Clone();
                    newBlock.Text = string.Empty;

                    foreach (var line in lines)
                    {
                        if (line.IsMatch)
                        {
                            if (!string.IsNullOrEmpty(newBlock.Text))
                            {
                                yield return newBlock;
                                newBlock = block.Clone();
                            }
                            newBlock.Text = line.LineInfo.Text;
                            newBlock.ContentKind = ContentKind.SoundEffect;
                        }
                        else
                        {
                            // Note: each paragraph has trailing whitespace
                            // so we can just concat the strings here
                            newBlock.Text += line.LineInfo.Text;
                        }
                    }

                    yield return newBlock;
                }
                else
                {
                    yield return block;
                }
            }
        }

        private IEnumerable<(bool IsMatch, TextLineInfo LineInfo)> GetLines(string text)
        {
            foreach (var line in GetTextLines(text))
            {
                bool matched = false;
                foreach (var regex in _regexes)
                {
                    if (regex.IsMatch(text))
                    {
                        matched = true;
                        break;
                    }
                }
                yield return (matched, line);
            }
        }*/
    }

    private sealed class ExtractActorTransform : ContentTransform
    {
        private readonly IReadOnlyList<Regex> _regexes = new[]
        {
            CreateRegex(@"^（(.+?)）(\w.+)$"),
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
            // TODO: inline TryExtractActor here
            if (TryExtractActor(text, out var actor, out var remainingText))
            {
                return SplitResult.Create(
                    true,
                    replacement: remainingText,
                    consumeFollowingContent: true,
                    metadata: actor);
            }
            return SplitResult.Create(false, metadata: default(string));
        }

        /*
        public override IEnumerable<TextBlock> ApplyTransform(TextBlock block)
        {
            if (block.ContentKind != ContentKind.Primary || string.IsNullOrEmpty(block.Text))
            {
                yield return block;
            }
            else
            {
                var doc = Markdown.Parse(block.Text);
                var paraInfo = ExtractParagraphInfo(doc);

                // TODO: this isn't handling LineBreakInline within paragraphs, which is how line breaks from subtitles are actually represented
                // need to handle a sequence of LiteralInlines (and maybe others, e.g. HtmlInline...) and preserve original LineBreakInlines

                if (paraInfo.All(p => p.Actor == null))
                {
                    yield return block;
                }
                else
                {
                    var newBlock = block.Clone();
                    newBlock.Text = string.Empty;

                    foreach (var para in paraInfo)
                    {
                        if (para.Actor != null)
                        {
                            if (!string.IsNullOrEmpty(newBlock.Text))
                            {
                                yield return CleanBlock(newBlock);
                                newBlock = block.Clone();
                            }
                            newBlock.Actor = para.Actor;
                            newBlock.Text = para.Text;
                        }
                        else
                        {
                            // Note: each paragraph has trailing whitespace
                            // so we can just concat the strings here
                            newBlock.Text += para.Text;
                        }
                    }

                    yield return CleanBlock(newBlock);
                }
            }
        }

        private IReadOnlyList<(string? Actor, string Text)> ExtractParagraphInfo(MarkdownDocument doc)
        {
            var array = new (string?, string)[doc.Count];
            for (int i = 0; i < doc.Count; i++)
            {
                var text = GetParagraphText(doc[i]);
                array[i] = TryExtractActor(text, out var actor, out var remainingText) ?
                    (actor, remainingText) :
                    (null, text);
            }
            return array;
        }

        private TextBlock CleanBlock(TextBlock block)
        {
            // Trim the extra whitespace added at the end by ParagraphBlock.ToMarkdownString()
            block.Text = block.Text?.TrimEnd();
            return block;
        }

        private string GetParagraphText(Block markdownBlock)
        {
            if (markdownBlock is ParagraphBlock para)
            {
                return para.ToMarkdownString();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected Markdown block: [{markdownBlock.GetType().Name}] {markdownBlock.ToMarkdownString()}");
            }
        }*/

        private bool TryExtractActor(
            string text,
            [NotNullWhen(true)] out string? actor,
            [NotNullWhen(true)] out string? remainingText)
        {
            foreach (var regex in _regexes)
            {
                var match = regex.Match(text);
                if (match.Success)
                {
                    actor = match.Groups[1].Value;
                    remainingText = match.Groups[2].Value;
                    return true;
                }
            }
            actor = null;
            remainingText = null;
            return false;
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
