namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;
using Tandoku.Content;
using Tandoku.Content.Transforms;

public sealed partial class Program
{
    private Command CreateContentCommand() =>
        new("content", "Commands for working with tandoku content streams")
        {
            this.CreateContentIndexCommand(),
            this.CreateContentSearchCommand(),
            this.CreateContentLinkCommand(),
            new ContentTransforms(this).CreateContentTransformCommand(),
        };

    private Command CreateContentIndexCommand()
    {
        var pathArgument = new Argument<DirectoryInfo>("path", "Path of content directory to index") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path", "Path of the index to build") { IsRequired = true }
            .LegalFilePathsOnly();

        var command = new Command("index", "Indexes the specified content")
        {
            pathArgument,
            indexPathOption,
        };

        command.SetHandler(async (path, indexPath) =>
        {
            var indexBuilder = new ContentIndexBuilder(this.fileSystem);
            await indexBuilder.BuildAsync(path.FullName, indexPath.FullName);
            this.console.WriteLine($"Wrote index to {indexPath}");
        }, pathArgument, indexPathOption);

        return command;
    }

    private Command CreateContentSearchCommand()
    {
        var searchQueryArgument = new Argument<string[]>("search-query", "Terms or phrase to search for") { Arity = ArgumentArity.OneOrMore };
        var maxHitsOption = new Option<int>(["--max-hits", "-n"], "Maximum number of results to return");
        var indexPathOption = new Option<DirectoryInfo>("--index-path", "Path of the index to use") { IsRequired = true }
            .LegalFilePathsOnly();

        var command = new Command("search", "Searches the specified content index")
        {
            searchQueryArgument,
            maxHitsOption,
            indexPathOption,
        };

        command.SetHandler(async (searchQuery, maxHits, indexPath) =>
        {
            var indexSearcher = new ContentIndexSearcher(this.fileSystem);
            var matchedBlockCount = 0;
            await foreach(var matchedBlock in indexSearcher.FindBlocksAsync(string.Join(' ', searchQuery), indexPath.FullName, maxHits))
            {
                matchedBlockCount++;
                this.console.WriteLine($"Matched {matchedBlock}");
            }
            this.console.WriteLine($"Matched {matchedBlockCount} total blocks");
        }, searchQueryArgument, maxHitsOption, indexPathOption);

        return command;
    }

    private Command CreateContentLinkCommand()
    {
        var inputPathArgument = new Argument<DirectoryInfo>("input-path", "Path of input content directory") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var outputPathArgument = new Argument<DirectoryInfo>("output-path", "Path of output content directory") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path", "Path of the index to use") { IsRequired = true }
            .LegalFilePathsOnly();
        var linkNameOption = new Option<string>("--link-name", "Name of the link") { IsRequired = true };

        var command = new Command("link", "Links the specified content to content in linked volumes")
        {
            inputPathArgument,
            outputPathArgument,
            indexPathOption,
            linkNameOption,
        };

        command.SetHandler(async (inputPath, outputPath, indexPath, linkName) =>
        {
            var linker = new ContentLinker(this.fileSystem);
            var stats = await linker.LinkAsync(inputPath.FullName, outputPath.FullName, indexPath.FullName, linkName);
            this.console.WriteLine($"Linked {stats.LinkedBlocks}/{stats.TotalBlocks} blocks ({stats.LinkedBlocks / (double)stats.TotalBlocks:p2})");
        }, inputPathArgument, outputPathArgument, indexPathOption, linkNameOption);

        return command;
    }

    private sealed class ContentTransforms(Program program)
    {
        internal Command CreateContentTransformCommand() =>
            new("transform", "Commands for transforming tandoku content streams")
            {
                this.CreateRemoveNonJapaneseTextCommand(),
                this.CreateLowConfidenceTextCommand(),
                this.CreateGenerateExplanationCommand(),
            };

        private Command CreateRemoveNonJapaneseTextCommand()
        {
            var commonArgsBinder = new CommonArgsBinder();

            var command = new Command("remove-non-japanese-text", "Removes text blocks without any Japanese text")
            {
                commonArgsBinder,
            };

            command.SetHandler(async (commonArgs) =>
            {
                await this.RunContentTransformAsync(
                    commonArgs,
                    t => t.TransformAsync(new RemoveNonJapaneseTextTransform()));
            }, commonArgsBinder);

            return command;
        }

        private Command CreateLowConfidenceTextCommand()
        {
            const double DefaultConfidenceThreshold = 0.8;

            var commonArgsBinder = new CommonArgsBinder();
            var confidenceThresholdOption = new Option<double>(
                ["--confidence-threshold", "--confidence", "-c"],
                description: "Confidence threshold for text to retain from image segments",
                getDefaultValue: () => DefaultConfidenceThreshold);

            var command = new Command("remove-low-confidence-text", "Removes text from low confidence image segments")
            {
                commonArgsBinder,
                confidenceThresholdOption,
            };

            command.SetHandler(async (commonArgs, confidenceThreshold) =>
            {
                await this.RunContentTransformAsync(
                    commonArgs,
                    t => t.TransformAsync(new RemoveLowConfidenceTextTransform(confidenceThreshold)));
            }, commonArgsBinder, confidenceThresholdOption);

            return command;
        }

        private Command CreateGenerateExplanationCommand()
        {
            var commonArgsBinder = new CommonArgsBinder();

            var command = new Command("generate-explanation", "Uses OpenAI to generate an explanation for each content block")
            {
                commonArgsBinder,
            };

            command.SetHandler(async (commonArgs) =>
            {
                using var transform = new GenerateExplanationTransform();
                await this.RunContentTransformAsync(
                    commonArgs,
                    t => t.TransformAsync(transform));
            }, commonArgsBinder);

            return command;
        }
        private Task RunContentTransformAsync(CommonArgs args, Func<ContentTransformer, Task> transform) =>
            transform(new ContentTransformer(args.InputPath.FullName, args.OutputPath.FullName, program.fileSystem));

        private sealed record CommonArgs(DirectoryInfo InputPath, DirectoryInfo OutputPath);

        private sealed class CommonArgsBinder : BinderBase<CommonArgs>, ICommandBinder
        {
            private readonly Argument<DirectoryInfo> inputPathArgument = new Argument<DirectoryInfo>(
                "input-path",
                "Path of input content directory") { Arity = ArgumentArity.ExactlyOne }
                .LegalFilePathsOnly();
            private readonly Argument<DirectoryInfo> outputPathArgument = new Argument<DirectoryInfo>(
                "output-path",
                "Path of output content directory") { Arity = ArgumentArity.ExactlyOne }
                .LegalFilePathsOnly();

            public void AddToCommand(Command command)
            {
                command.Add(this.inputPathArgument);
                command.Add(this.outputPathArgument);
            }

            protected override CommonArgs GetBoundValue(BindingContext bindingContext)
            {
                var inputPath = bindingContext.ParseResult.GetValueForArgument(this.inputPathArgument);
                var outputPath = bindingContext.ParseResult.GetValueForArgument(this.outputPathArgument);
                return new CommonArgs(inputPath, outputPath);
            }
        }
    }
}
