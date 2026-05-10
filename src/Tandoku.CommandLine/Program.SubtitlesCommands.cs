namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Subtitles;

public sealed partial class Program
{
    private Command CreateSubtitlesCommand() =>
        new("subtitles", "Commands for working with subtitle files")
        {
            this.CreateSubtitlesGenerateContentCommand(),
            this.CreateSubtitlesGenerateCommand(),
            this.CreateSubtitlesTtmlToWebVttCommand(),
        };

    private Command CreateSubtitlesGenerateContentCommand()
    {
        var inputPathArgument = ArgumentFactory.InputPath();
        var outputPathArgument = ArgumentFactory.OutputPath();

        var command = new Command("generate-content", "Generates tandoku content from subtitles")
        {
            inputPathArgument,
            outputPathArgument,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
            var generator = new SubtitleContentGenerator(
                inputPath.FullName,
                outputPath.FullName,
                this.fileSystem);
            await generator.GenerateContentAsync();
        });

        return command;
    }

    private Command CreateSubtitlesGenerateCommand()
    {
        var inputPathArgument = ArgumentFactory.InputPath();
        var outputPathArgument = ArgumentFactory.OutputPath();
        var purposeOption = new Option<SubtitlePurpose>("--purpose")
        {
            Description = "Purpose of generated subtitles"
        };
        var includeRefOption = new Option<string?>("--include-ref")
        {
            Description = "Include subtitles for blocks with the specified reference"
        };
        var extendAudioOption = new Option<int>("--extend-audio")
        {
            Description = "Extend audio clips by specified duration (in msecs)"
        };

        var command = new Command("generate", "Generates subtitles from tandoku content")
        {
            inputPathArgument,
            outputPathArgument,
            purposeOption,
            includeRefOption,
            extendAudioOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
            var (purpose, includeRef, extendAudio) = parseResult.GetValues(purposeOption, includeRefOption, extendAudioOption);

            var generator = new SubtitleGenerator(purpose, includeRef, extendAudio, this.fileSystem);
            await generator.GenerateAsync(inputPath.FullName, outputPath.FullName);
        });

        return command;
    }

    private Command CreateSubtitlesTtmlToWebVttCommand()
    {
        var inputPathArgument = ArgumentFactory.InputPath();
        var outputPathArgument = ArgumentFactory.OutputPath();

        var command = new Command("ttml-to-webvtt", "Converts TTML subtitles to WebVTT format")
        {
            inputPathArgument,
            outputPathArgument,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var (inputPath, outputPath) = parseResult.GetRequiredValues(inputPathArgument, outputPathArgument);
            var converter = new TtmlToWebVttConverter(
                inputPath.FullName,
                outputPath.FullName,
                this.fileSystem);
            await converter.ConvertAsync();
        });

        return command;
    }
}
