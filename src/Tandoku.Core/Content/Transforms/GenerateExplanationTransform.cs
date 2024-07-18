namespace Tandoku.Content.Transforms;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenAI.Chat;
using Tandoku.Serialization;

public sealed partial class GenerateExplanationTransform : ContentBlockRewriter, IDisposable
{
    private const string OpenAIModel = "gpt-4o";

    private readonly ChatClientWrapper chatClient;
    private readonly SystemChatMessage systemMessage;
    private readonly ChatCompletionOptions chatCompletionOptions;

    private int blockCount;

    public GenerateExplanationTransform()
    {
        // TODO - cacheOnly option to only return completions from cache
        // AND/OR redesign the whole system of capturing output so we can actually check in the raw completions
        //System.Diagnostics.Debugger.Launch();
        this.chatClient = new ChatClientWrapper(OpenAIModel);
        // TODO - "sentence" variation of prompt for input that is already in paragraph blocks
        this.systemMessage = new SystemChatMessage(File.ReadAllText(@"c:\users\aaron\repos\tandoku\resources\prompts\explanation.txt"));
        this.chatCompletionOptions = new ChatCompletionOptions
        {
            MaxTokens = 4000,
            Temperature = 0.3f,
        };
    }

    public void Dispose()
    {
        this.chatClient.Dispose();
    }

    public override ContentBlock? Visit(CompositeBlock block)
    {
        var text = string.Join('\n', block.Blocks.Select(b => b.Text));

        this.blockCount++;
        if (this.blockCount > 6)
        {
            Console.Error.WriteLine($"Skipping block {this.blockCount}");
            return block;
        }
        Console.Error.WriteLine($"Processing block {this.blockCount}");
        foreach (var nestedBlock in block.Blocks)
            Console.Error.WriteLine($"  {nestedBlock.Text}");

        var userMessage = new UserChatMessage(text);
        var resultText = this.chatClient.CompleteChat([this.systemMessage, userMessage], this.chatCompletionOptions);
        if (string.IsNullOrEmpty(resultText))
            return block;

        // TODO - switch to using regex to extract all content (and add ** back around words)
        var yaml = ExtractYamlRegex().Match(resultText).Groups[2].Value;

        var blockEnumerator = block.Blocks.GetEnumerator();
        var newBlocks = ImmutableList.CreateBuilder<TextBlock>();
        foreach (var explanation in YamlSerializer.ReadStreamAsync<ExplanationDocument>(new StringReader(yaml)).ToBlockingEnumerable())
        {
            if (!blockEnumerator.MoveNext())
                break;

            var currentBlock = blockEnumerator.Current;
            // TODO - clean this up and make it more relaxed (allow sentence to start in the middle of a block)
            while (currentBlock.Text is null || !CleanText(explanation.Sentence).StartsWith(CleanText(currentBlock.Text)))
            {
                Console.Error.WriteLine("Mismatched block:");
                Console.Error.WriteLine($" Block: {currentBlock.Text}");
                Console.Error.WriteLine($" Sentence: {explanation.Sentence}");

                newBlocks.Add(currentBlock);

                if (!blockEnumerator.MoveNext())
                    break;
                
                currentBlock = blockEnumerator.Current;
            }

            currentBlock = currentBlock with
            {
                References = currentBlock.References.Add(
                    "ex",
                    new ContentReference
                    {
                        Text = $"{string.Join('\n', explanation.Words.Select(w => $"- {w}"))}\n\n{explanation.Translation}",
                    })
            };

            newBlocks.Add(currentBlock);
        }

        return block with
        {
            Blocks = newBlocks.ToImmutable(),
        };
    }

    private static string CleanText(string text) => text.Trim().Replace(" ", "").Replace("「", "").Replace("」", "");

    [GeneratedRegex("^(```yaml)?(.+?)(```)?$", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ExtractYamlRegex();

    private record ExplanationDocument : IYamlStreamSerializable<ExplanationDocument>
    {
        [JsonPropertyName("line")]
        public required string Sentence { get; init;  }

        public IImmutableList<string> Words { get; init; } = [];

        public required string Translation { get; init;  }
    }

    private sealed class ChatClientWrapper : IDisposable
    {
        private readonly ChatClient chatClient;
        private readonly string cachePath;
        private readonly Dictionary<string, string> cache = [];

        internal ChatClientWrapper(string model)
        {
            this.chatClient = new(model);
            this.cachePath = $@"temp/openaicache-{model}.json";
            if (File.Exists(this.cachePath))
            {
                using var stream = File.OpenRead(this.cachePath);
                this.cache = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            }
        }

        internal string CompleteChat(IEnumerable<ChatMessage> messages, ChatCompletionOptions options)
        {
            var cacheKey = GetCompletionKey(messages);
            if (this.cache.TryGetValue(cacheKey, out var resultText))
            {
                return resultText;
            }

            // TODO - cache only option

            var result = this.chatClient.CompleteChat(messages, options);
            resultText = result.Value.ToString();
            this.cache.Add(cacheKey, resultText);
            return resultText;
        }

        private static string GetCompletionKey(IEnumerable<ChatMessage> messages)
        {
            return JsonSerializer.Serialize(messages, SerializationFactory.JsonOptions);
        }

        public void Dispose()
        {
            using var stream = File.OpenWrite(this.cachePath);
            JsonSerializer.Serialize(stream, this.cache);
        }
    }
}
