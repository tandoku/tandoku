namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Content;
using Tandoku.Content.Alignment;
using Tandoku.Content.Transforms;

public sealed partial class Program
{
    private Command CreateContentCommand() =>
        new("content", "Commands for working with tandoku content streams")
        {
            this.CreateContentIndexCommand(),
            this.CreateContentSearchCommand(),
            this.CreateContentLinkCommand(),
            this.CreateContentMergeCommand(),
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

    private Command CreateContentMergeCommand()
    {
        var inputPathArgument = new Argument<DirectoryInfo>("input-path", "Path of input content directory") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var refPathArgument = new Argument<DirectoryInfo>("ref-path", "Path of reference content directory") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var outputPathArgument = new Argument<DirectoryInfo>("output-path", "Path of output content directory") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var alignOption = new Option<ContentAlignmentKind>("--align", "Alignment algorithm") { IsRequired = true };
        var refNameOption = new Option<string>(["--ref", "--reference-name"], "Name of reference in merged content") { IsRequired = true };

        var command = new Command("merge", "Merges the specified content with reference content")
        {
            inputPathArgument,
            refPathArgument,
            outputPathArgument,
            alignOption,
            refNameOption,
        };

        command.SetHandler(async (inputPath, refPath, outputPath, alignmentKind, refName) =>
        {
            var merger = new ContentMerger(this.fileSystem);
            var aligner = CreateContentAligner(alignmentKind, refName);
            await merger.MergeAsync(inputPath.FullName, refPath.FullName, outputPath.FullName, aligner);
        }, inputPathArgument, refPathArgument, outputPathArgument, alignOption, refNameOption);

        return command;
    }

    private static IContentAligner CreateContentAligner(ContentAlignmentKind alignmentKind, string refName)
    {
        return alignmentKind switch
        {
            ContentAlignmentKind.Timecodes => new TimecodeContentAligner(refName),
            _ => throw new ArgumentOutOfRangeException(nameof(alignmentKind)),
        };
    }

    private enum ContentAlignmentKind
    {
        Timecodes,
    }

    private sealed class ContentTransforms(Program program)
    {
        internal Command CreateContentTransformCommand() =>
            new("transform", "Commands for transforming tandoku content streams")
            {
                this.CreateRemoveNonJapaneseTextCommand(),
                this.CreateLowConfidenceTextCommand(),
                this.CreateImportMediaCommand(),
                this.CreateImportImageTextCommand(),
            };

        private Command CreateRemoveNonJapaneseTextCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();

            var command = new Command("remove-non-japanese-text", "Removes text blocks without any Japanese text")
            {
                pathArgsBinder,
            };

            command.SetHandler(async (pathArgs) =>
            {
                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(new RemoveNonJapaneseTextTransform()));
            }, pathArgsBinder);

            return command;
        }

        private Command CreateLowConfidenceTextCommand()
        {
            const double DefaultConfidenceThreshold = 0.8;

            var pathArgsBinder = new InputOutputPathArgsBinder();
            var confidenceThresholdOption = new Option<double>(
                ["--confidence-threshold", "--confidence", "-c"],
                description: "Confidence threshold for text to retain from image segments",
                getDefaultValue: () => DefaultConfidenceThreshold);

            var command = new Command("remove-low-confidence-text", "Removes text from low confidence image segments")
            {
                pathArgsBinder,
                confidenceThresholdOption,
            };

            command.SetHandler(async (pathArgs, confidenceThreshold) =>
            {
                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(new RemoveLowConfidenceTextTransform(confidenceThreshold)));
            }, pathArgsBinder, confidenceThresholdOption);

            return command;
        }

        private Command CreateImportMediaCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();
            var mediaPathOption = new Option<DirectoryInfo>("--media-path", "Path of the media") { IsRequired = true }
                .LegalFilePathsOnly();
            var imagePrefixOption = new Option<string?>("--image-prefix", "Prefix to include for image names");
            var audioPrefixOption = new Option<string?>("--audio-prefix", "Prefix to include for audio names");

            var command = new Command("import-media", "Imports media from the specified path into the content")
            {
                pathArgsBinder,
                mediaPathOption,
                imagePrefixOption,
                audioPrefixOption,
            };

            command.SetHandler(async (pathArgs, mediaPath, imagePrefix, audioPrefix, jsonOutput) =>
            {
                var mediaCollection = new MediaCollection();
                var transform = new ImportMediaTransform(
                    mediaPath.FullName,
                    imagePrefix,
                    audioPrefix,
                    mediaCollection,
                    program.fileSystem);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(transform));

                if (jsonOutput)
                {
                    program.console.WriteJsonOutput(mediaCollection);
                }
                else
                {
                    // TODO - YAML output
                }
            }, pathArgsBinder, mediaPathOption, imagePrefixOption, audioPrefixOption, program.jsonOutputOption);

            return command;
        }

        private Command CreateImportImageTextCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();
            var providerOption = new Option<ImageAnalysisProvider>(["--provider", "-p"], "Image analysis provider") { IsRequired = true };
            var volumeBinder = program.CreateVolumeBinder();

            var command = new Command("import-image-text", "Imports analyzed image text into the content")
            {
                pathArgsBinder,
                providerOption,
                volumeBinder,
            };

            command.SetHandler(async (pathArgs, provider, volumeDirectory) =>
            {
                var volumeManager = program.CreateVolumeManager();
                var volumeInfo = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
                var transform = new ImportImageTextTransform(provider, volumeInfo, program.fileSystem);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(transform));
            }, pathArgsBinder, providerOption, volumeBinder);

            return command;
        }

        private Task RunContentTransformAsync(InputOutputPathArgs args, Func<ContentTransformer, Task> transform) =>
            transform(new ContentTransformer(args.InputPath.FullName, args.OutputPath.FullName, program.fileSystem));
    }
}
