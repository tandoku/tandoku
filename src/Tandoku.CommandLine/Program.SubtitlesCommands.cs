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
        var pathArgsBinder = new InputOutputPathArgsBinder();

        var command = new Command("generate-content", "Generates tandoku content from subtitles")
        {
            pathArgsBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pathArgs = parseResult.GetValue(pathArgsBinder);
            var generator = new SubtitleContentGenerator(
                pathArgs.InputPath.FullName,
                pathArgs.OutputPath.FullName,
                this.fileSystem);
            await generator.GenerateContentAsync();
        });

        return command;
    }

    private Command CreateSubtitlesGenerateCommand()
    {
        var pathArgsBinder = new InputOutputPathArgsBinder();
        var purposeOption = new Option<SubtitlePurpose>("--purpose") { Description = "Purpose of generated subtitles" };
        var includeRefOption = new Option<string?>("--include-ref") { Description = "Include subtitles for blocks with the specified reference" };
        var extendAudioOption = new Option<int>("--extend-audio") { Description = "Extend audio clips by specified duration (in msecs)" };

        var command = new Command("generate", "Generates subtitles from tandoku content")
        {
            pathArgsBinder,
            purposeOption,
            includeRefOption,
            extendAudioOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var (pathArgs, purpose, includeRef, extendAudio) = parseResult.GetValues(pathArgsBinder, purposeOption, includeRefOption, extendAudioOption);

            var generator = new SubtitleGenerator(purpose, includeRef, extendAudio, this.fileSystem);
            await generator.GenerateAsync(pathArgs.InputPath.FullName, pathArgs.OutputPath.FullName);
        });

        return command;
    }

    private Command CreateSubtitlesTtmlToWebVttCommand()
    {
        var pathArgsBinder = new InputOutputPathArgsBinder();

        var command = new Command("ttml-to-webvtt", "Converts TTML subtitles to WebVTT format")
        {
            pathArgsBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pathArgs = parseResult.GetValue(pathArgsBinder);
            var converter = new TtmlToWebVttConverter(
                pathArgs.InputPath.FullName,
                pathArgs.OutputPath.FullName,
                this.fileSystem);
            await converter.ConvertAsync();
        });

        return command;
    }
}
