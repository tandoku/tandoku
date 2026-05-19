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
        var pathArgument = new Argument<DirectoryInfo>("path")
        {
            Description = "Path of content directory to index",
            Arity = ArgumentArity.ExactlyOne
        }.AcceptLegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path")
        {
            Description = "Path of the index to build",
            Required = true
        }.AcceptLegalFilePathsOnly();

        var command = new Command("index", "Indexes the specified content")
        {
            pathArgument,
            indexPathOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetRequiredValue(pathArgument);
            var indexPath = parseResult.GetRequiredValue(indexPathOption);

            var indexBuilder = new ContentIndexBuilder(this.fileSystem);
            await indexBuilder.BuildAsync(path.FullName, indexPath.FullName);
            this.output.WriteLine($"Wrote index to {indexPath}");
        });

        return command;
    }

    private Command CreateContentSearchCommand()
    {
        var searchQueryArgument = new Argument<string[]>("search-query")
        {
            Description = "Terms or phrase to search for",
            Arity = ArgumentArity.OneOrMore
        };
        var indexPathOption = new Option<DirectoryInfo>("--index-path")
        {
            Description = "Path of the index to use",
            Required = true
        }.AcceptLegalFilePathsOnly();
        var maxHitsOption = new Option<int?>("--max-hits", "-n")
        {
            Description = "Maximum number of results to return"
        };

        var command = new Command("search", "Searches the specified content index")
        {
            searchQueryArgument,
            indexPathOption,
            maxHitsOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var searchQuery = parseResult.GetRequiredValue(searchQueryArgument);
            var indexPath = parseResult.GetRequiredValue(indexPathOption);
            var maxHits = parseResult.GetValue(maxHitsOption);

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
        var inputPathArgument = ArgumentFactory.InputPath();
        var outputPathArgument = ArgumentFactory.OutputPath();
        var indexPathOption = new Option<DirectoryInfo>("--index-path")
        {
            Description = "Path of the index to use",
            Required = true
        }.AcceptLegalFilePathsOnly();
        var linkNameOption = new Option<string>("--link-name")
        {
            Description = "Name of the link",
            Required = true
        };

        var command = new Command("link", "Links the specified content to content in linked volumes")
        {
            inputPathArgument,
            outputPathArgument,
            indexPathOption,
            linkNameOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
            var (indexPath, linkName) = parseResult.GetRequiredValues(indexPathOption, linkNameOption);

            var linker = new ContentLinker(this.fileSystem);
            var stats = await linker.LinkAsync(inputPath.FullName, outputPath.FullName, indexPath.FullName, linkName);
            this.output.WriteLine($"Linked {stats.LinkedBlocks}/{stats.TotalBlocks} blocks ({stats.LinkedBlocks / (double)stats.TotalBlocks:p2})");
        });

        return command;
    }

    private Command CreateContentMergeCommand()
    {
        var inputPathArgument = ArgumentFactory.InputPath();
        var refPathArgument = new Argument<DirectoryInfo>("ref-path")
        {
            Description = "Path of reference content directory",
            Arity = ArgumentArity.ExactlyOne
        }.AcceptLegalFilePathsOnly();
        var outputPathArgument = ArgumentFactory.OutputPath();
        var alignOption = new Option<ContentAlignmentKind>("--align")
        {
            Description = "Alignment algorithm",
            Required = true
        };
        var refNameOption = new Option<string>("--ref", "--reference-name")
        {
            Description = "Name of reference in merged content",
            Required = true
        };

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
            var (inputPath, refPath, outputPath) = parseResult.GetRequiredValues(
                inputPathArgument,
                refPathArgument,
                outputPathArgument);
            var (alignmentKind, refName) = parseResult.GetRequiredValues(alignOption, refNameOption);

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
                this.CreateGroupSimilarImagesCommand(),
            };

        private Command CreateRemoveNonJapaneseTextCommand()
        {
            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();
            var roleOption = new NullableEnumOption<ChunkRole>("--role", "-r")
            {
                Description = "Only remove chunks with the specified role",
            };

            var command = new Command("remove-non-japanese-text", "Removes chunks without any Japanese text")
            {
                inputPathArgument,
                outputPathArgument,
                roleOption,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
                var role = parseResult.GetValue(roleOption);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
                    t => t.TransformAsync(new RemoveNonJapaneseTextTransform(role)));
            });

            return command;
        }

        private Command CreateLowConfidenceTextCommand()
        {
            const double DefaultConfidenceThreshold = 0.8;

            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();
            var confidenceThresholdOption = new Option<double>("--confidence-threshold", "--confidence", "-c")
            {
                Description = "Confidence threshold for text to retain from image segments",
                DefaultValueFactory = _ => DefaultConfidenceThreshold,
            };

            var command = new Command("remove-low-confidence-text", "Removes text from low confidence image segments")
            {
                inputPathArgument,
                outputPathArgument,
                confidenceThresholdOption,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
                var confidenceThreshold = parseResult.GetValue(confidenceThresholdOption);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
                    t => t.TransformAsync(new RemoveLowConfidenceTextTransform(confidenceThreshold)));
            });

            return command;
        }

        private Command CreateImportMediaCommand()
        {
            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();
            var mediaPathOption = new Option<DirectoryInfo>("--media-path")
            {
                Description = "Path of the media",
                Required = true
            }.AcceptLegalFilePathsOnly();
            var imagePrefixOption = new Option<string?>("--image-prefix")
            {
                Description = "Prefix to include for image names"
            };
            var audioPrefixOption = new Option<string?>("--audio-prefix")
            {
                Description = "Prefix to include for audio names"
            };

            var command = new Command("import-media", "Imports media from the specified path into the content")
            {
                inputPathArgument,
                outputPathArgument,
                mediaPathOption,
                imagePrefixOption,
                audioPrefixOption,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
                var mediaPath = parseResult.GetRequiredValue(mediaPathOption);
                var (imagePrefix, audioPrefix) = parseResult.GetValues(imagePrefixOption, audioPrefixOption);
                var jsonOutput = parseResult.GetValue(program.jsonOutputOption);

                var mediaCollection = new MediaCollection();
                var transform = new ImportMediaTransform(
                    mediaPath.FullName,
                    imagePrefix,
                    audioPrefix,
                    mediaCollection,
                    program.fileSystem);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
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
            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();
            var providerOption = new Option<ImageAnalysisProvider>("--provider", "-p")
            {
                Description = "Image analysis provider",
                Required = true
            };
            var roleOption = new NullableEnumOption<ChunkRole>("--role", "-r")
            {
                Description = "Sets the specified role on recognized text",
            };
            var volumeBinder = program.CreateVolumeBinder();

            var command = new Command("import-image-text", "Imports analyzed image text into the content")
            {
                inputPathArgument,
                outputPathArgument,
                providerOption,
                roleOption,
                volumeBinder,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
                var (provider, role) = parseResult.GetValues(providerOption, roleOption);
                var volumeDirectory = parseResult.GetValue(volumeBinder);

                var volumeManager = program.CreateVolumeManager();
                var volumeInfo = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
                var analyisProvider = CreateImageAnalysisProvider(provider);
                var transform = new ImportImageTextTransform(analyisProvider, volumeInfo, role, program.fileSystem);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
                    t => t.TransformAsync(transform));
            });

            return command;
        }

        private Command CreateMergeRefChunksCommand()
        {
            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();

            var command = new Command("merge-ref-chunks", "Merges reference chunks into following chunks")
            {
                inputPathArgument,
                outputPathArgument,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
                    t => t.TransformAsync(new MergeRefChunksTransform()));
            });

            return command;
        }

        private Task RunContentTransformAsync(DirectoryInfo inputPath, DirectoryInfo outputPath, Func<ContentTransformer, Task> transform) =>
            transform(new ContentTransformer(inputPath.FullName, outputPath.FullName, program.fileSystem));

        private Command CreateGroupSimilarImagesCommand()
        {
            const double DefaultSimilarityThreshold = 0.9;

            var inputPathArgument = ArgumentFactory.InputPath();
            var outputPathArgument = ArgumentFactory.OutputPath();
            var similarityThresholdOption = new Option<double>("--similarity-threshold", "--similarity", "-s")
            {
                Description = "Similarity threshold (0.0-1.0) at which images are grouped",
                DefaultValueFactory = _ => DefaultSimilarityThreshold,
            };
            var volumeBinder = program.CreateVolumeBinder();

            var command = new Command("group-similar-images", "Annotates blocks whose image is similar to the prior block's (or group's) image")
            {
                inputPathArgument,
                outputPathArgument,
                similarityThresholdOption,
                volumeBinder,
            };

            command.SetAction(async (parseResult, ct) =>
            {
                var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
                var similarityThreshold = parseResult.GetRequiredValue(similarityThresholdOption);
                var volumeDirectory = parseResult.GetValue(volumeBinder);

                var volumeManager = program.CreateVolumeManager();
                var volumeInfo = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
                var provider = new AverageHashImageSimilarityProvider();
                var transform = GroupSimilarImagesTransform.Create(
                    provider,
                    similarityThreshold,
                    volumeInfo,
                    program.fileSystem);

                await this.RunContentTransformAsync(
                    inputPath,
                    outputPath,
                    t => t.TransformAsync(transform));
            });

            return command;
        }

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
