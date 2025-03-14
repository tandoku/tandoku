﻿namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Subtitles;

public sealed partial class Program
{
    private Command CreateSubtitlesCommand() =>
        new("subtitles", "Commands for working with subtitle files")
        {
            this.CreateSubtitlesGenerateContentCommand(),
            this.CreateSubtitlesGenerateCommand(),
        };

    private Command CreateSubtitlesGenerateContentCommand()
    {
        var pathArgsBinder = new InputOutputPathArgsBinder();

        var command = new Command("generate-content", "Generates tandoku content from subtitles")
        {
            pathArgsBinder,
        };

        command.SetHandler(async (InputOutputPathArgs pathArgs) =>
        {
            var generator = new SubtitleContentGenerator(
                pathArgs.InputPath.FullName,
                pathArgs.OutputPath.FullName,
                this.fileSystem);
            await generator.GenerateContentAsync();
        }, pathArgsBinder);

        return command;
    }

    private Command CreateSubtitlesGenerateCommand()
    {
        var pathArgsBinder = new InputOutputPathArgsBinder();
        var purposeOption = new Option<SubtitlePurpose>("--purpose", "Purpose of generated subtitles");
        var includeRefOption = new Option<string?>("--include-ref", "Include subtitles for blocks with the specified reference");
        var extendAudioOption = new Option<int>("--extend-audio", "Extend audio clips by specified duration (in msecs)");

        var command = new Command("generate", "Generates subtitles from tandoku content")
        {
            pathArgsBinder,
            purposeOption,
            includeRefOption,
            extendAudioOption,
        };

        command.SetHandler(async (pathArgs, purpose, includeRef, extendAudio) =>
        {
            var generator = new SubtitleGenerator(purpose, includeRef, extendAudio, this.fileSystem);
            await generator.GenerateAsync(pathArgs.InputPath.FullName, pathArgs.OutputPath.FullName);
        }, pathArgsBinder, purposeOption, includeRefOption, extendAudioOption);

        return command;
    }
}
