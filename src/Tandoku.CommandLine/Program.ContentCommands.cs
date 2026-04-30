namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Content;
using Tandoku.Content.Alignment;
using Tandoku.Content.Transforms;
using Tandoku.Images;

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
        var pathArgument = new Argument<DirectoryInfo>("path") { Description = "Path of content directory to index", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path") { Description = "Path of the index to build", Required = true }.LegalFilePathsOnly();

        var command = new Command("index", "Indexes the specified content")
        {
            pathArgument,
            indexPathOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var indexPath = parseResult.GetValue(indexPathOption)!;

            var indexBuilder = new ContentIndexBuilder(this.fileSystem);
            await indexBuilder.BuildAsync(path.FullName, indexPath.FullName);
            this.output.WriteLine($"Wrote index to {indexPath}");
        });

        return command;
    }

    private Command CreateContentSearchCommand()
    {
        var searchQueryArgument = new Argument<string[]>("search-query") { Description = "Terms or phrase to search for", Arity = ArgumentArity.OneOrMore };
        var maxHitsOption = new Option<int>("--max-hits", "-n") { Description = "Maximum number of results to return" };
        var indexPathOption = new Option<DirectoryInfo>("--index-path") { Description = "Path of the index to use", Required = true }.LegalFilePathsOnly();

        var command = new Command("search", "Searches the specified content index")
        {
            searchQueryArgument,
            maxHitsOption,
            indexPathOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var searchQuery = parseResult.GetValue(searchQueryArgument)!;
            var maxHits = parseResult.GetValue(maxHitsOption);
            var indexPath = parseResult.GetValue(indexPathOption)!;

            var indexSearcher = new ContentIndexSearcher(this.fileSystem);
            var matchedBlockCount = 0;
            await foreach(var matchedBlock in indexSearcher.FindBlocksAsync(string.Join(' ', searchQuery), indexPath.FullName, maxHits))
            {
                matchedBlockCount++;
                this.output.WriteLine($"Matched {matchedBlock}");
            }
            this.output.WriteLine($"Matched {matchedBlockCount} total blocks");
        });

        return command;
    }

    private Command CreateContentLinkCommand()
    {
        var inputPathArgument = new Argument<DirectoryInfo>("input-path") { Description = "Path of input content directory", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var outputPathArgument = new Argument<DirectoryInfo>("output-path") { Description = "Path of output content directory", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path") { Description = "Path of the index to use", Required = true }.LegalFilePathsOnly();
        var linkNameOption = new Option<string>("--link-name") { Description = "Name of the link", Required = true };

        var command = new Command("link", "Links the specified content to content in linked volumes")
        {
            inputPathArgument,
            outputPathArgument,
            indexPathOption,
            linkNameOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var inputPath = parseResult.GetValue(inputPathArgument)!;
            var outputPath = parseResult.GetValue(outputPathArgument)!;
            var indexPath = parseResult.GetValue(indexPathOption)!;
            var linkName = parseResult.GetValue(linkNameOption)!;

            var linker = new ContentLinker(this.fileSystem);
            var stats = await linker.LinkAsync(inputPath.FullName, outputPath.FullName, indexPath.FullName, linkName);
            this.output.WriteLine($"Linked {stats.LinkedBlocks}/{stats.TotalBlocks} blocks ({stats.LinkedBlocks / (double)stats.TotalBlocks:p2})");
        });

        return command;
    }

    private Command CreateContentMergeCommand()
    {
        var inputPathArgument = new Argument<DirectoryInfo>("input-path") { Description = "Path of input content directory", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var refPathArgument = new Argument<DirectoryInfo>("ref-path") { Description = "Path of reference content directory", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var outputPathArgument = new Argument<DirectoryInfo>("output-path") { Description = "Path of output content directory", Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly();
        var alignOption = new Option<ContentAlignmentKind>("--align") { Description = "Alignment algorithm", Required = true };
        var refNameOption = new Option<string>("--ref", "--reference-name") { Description = "Name of reference in merged content", Required = true };

        var command = new Command("merge", "Merges the specified content with reference content")
        {
            inputPathArgument,
            refPathArgument,
            outputPathArgument,
            alignOption,
            refNameOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var inputPath = parseResult.GetValue(inputPathArgument)!;
            var refPath = parseResult.GetValue(refPathArgument)!;
            var outputPath = parseResult.GetValue(outputPathArgument)!;
            var alignmentKind = parseResult.GetValue(alignOption);
            var refName = parseResult.GetValue(refNameOption)!;

            var merger = new ContentMerger(this.fileSystem);
            var aligner = CreateContentAligner(alignmentKind, refName);
            await merger.MergeAsync(inputPath.FullName, refPath.FullName, outputPath.FullName, aligner);
        });

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
                this.CreateMergeRefChunksCommand(),
            };

        private Command CreateRemoveNonJapaneseTextCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();
            var roleOption = EnumOption.CreateNullable<ChunkRole>("--role", "Only remove chunks with the specified role", "-r");

            var command = new Command("remove-non-japanese-text", "Removes chunks without any Japanese text");
            pathArgsBinder.AddToCommand(command);
            command.Options.Add(roleOption);

            command.SetAction(async (parseResult, ct) =>
            {
                var pathArgs = pathArgsBinder.Resolve(parseResult);
                var role = parseResult.GetValue(roleOption);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(new RemoveNonJapaneseTextTransform(role)));
            });

            return command;
        }

        private Command CreateLowConfidenceTextCommand()
        {
            const double DefaultConfidenceThreshold = 0.8;

            var pathArgsBinder = new InputOutputPathArgsBinder();
            var confidenceThresholdOption = new Option<double>("--confidence-threshold", "--confidence", "-c")
            {
                Description = "Confidence threshold for text to retain from image segments",
                DefaultValueFactory = _ => DefaultConfidenceThreshold,
            };

            var command = new Command("remove-low-confidence-text", "Removes text from low confidence image segments");
            pathArgsBinder.AddToCommand(command);
            command.Options.Add(confidenceThresholdOption);

            command.SetAction(async (parseResult, ct) =>
            {
                var pathArgs = pathArgsBinder.Resolve(parseResult);
                var confidenceThreshold = parseResult.GetValue(confidenceThresholdOption);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(new RemoveLowConfidenceTextTransform(confidenceThreshold)));
            });

            return command;
        }

        private Command CreateImportMediaCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();
            var mediaPathOption = new Option<DirectoryInfo>("--media-path") { Description = "Path of the media", Required = true }.LegalFilePathsOnly();
            var imagePrefixOption = new Option<string?>("--image-prefix") { Description = "Prefix to include for image names" };
            var audioPrefixOption = new Option<string?>("--audio-prefix") { Description = "Prefix to include for audio names" };

            var command = new Command("import-media", "Imports media from the specified path into the content");
            pathArgsBinder.AddToCommand(command);
            command.Options.Add(mediaPathOption);
            command.Options.Add(imagePrefixOption);
            command.Options.Add(audioPrefixOption);

            command.SetAction(async (parseResult, ct) =>
            {
                var pathArgs = pathArgsBinder.Resolve(parseResult);
                var mediaPath = parseResult.GetValue(mediaPathOption)!;
                var imagePrefix = parseResult.GetValue(imagePrefixOption);
                var audioPrefix = parseResult.GetValue(audioPrefixOption);
                var jsonOutput = parseResult.GetValue(program.jsonOutputOption);

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
                    program.output.WriteJsonOutput(mediaCollection);
                }
                else
                {
                    // TODO - YAML output
                }
            });

            return command;
        }

        private Command CreateImportImageTextCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();
            var providerOption = new Option<ImageAnalysisProvider>("--provider", "-p") { Description = "Image analysis provider", Required = true };
            var roleOption = EnumOption.CreateNullable<ChunkRole>("--role", "Sets the specified role on recognized text", "-r");
            var volumeBinder = program.CreateVolumeBinder();

            var command = new Command("import-image-text", "Imports analyzed image text into the content");
            pathArgsBinder.AddToCommand(command);
            command.Options.Add(providerOption);
            command.Options.Add(roleOption);
            volumeBinder.AddToCommand(command);

            command.SetAction(async (parseResult, ct) =>
            {
                var pathArgs = pathArgsBinder.Resolve(parseResult);
                var provider = parseResult.GetValue(providerOption);
                var role = parseResult.GetValue(roleOption);
                var volumeDirectory = volumeBinder.Resolve(parseResult);

                var volumeManager = program.CreateVolumeManager();
                var volumeInfo = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
                var analyisProvider = CreateImageAnalysisProvider(provider);
                var transform = new ImportImageTextTransform(analyisProvider, volumeInfo, role, program.fileSystem);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(transform));
            });

            return command;
        }

        private Command CreateMergeRefChunksCommand()
        {
            var pathArgsBinder = new InputOutputPathArgsBinder();

            var command = new Command("merge-ref-chunks", "Merges reference chunks into following chunks");
            pathArgsBinder.AddToCommand(command);

            command.SetAction(async (parseResult, ct) =>
            {
                var pathArgs = pathArgsBinder.Resolve(parseResult);

                await this.RunContentTransformAsync(
                    pathArgs,
                    t => t.TransformAsync(new MergeRefChunksTransform()));
            });

            return command;
        }

        private Task RunContentTransformAsync(InputOutputPathArgs args, Func<ContentTransformer, Task> transform) =>
            transform(new ContentTransformer(args.InputPath.FullName, args.OutputPath.FullName, program.fileSystem));

        private static IImageAnalysisProvider CreateImageAnalysisProvider(ImageAnalysisProvider provider) =>
            provider switch
            {
                ImageAnalysisProvider.Acv4 => new Acv4ImageAnalysisProvider(),
                ImageAnalysisProvider.EasyOcr => new EasyOcrImageAnalysisProvider(),
                _ => throw new ArgumentOutOfRangeException(nameof(provider)),
            };

        private enum ImageAnalysisProvider
        {
            Acv4,
            EasyOcr
        }
    }
}
