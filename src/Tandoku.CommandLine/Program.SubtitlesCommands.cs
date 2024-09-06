namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Subtitles;

public sealed partial class Program
{
    private Command CreateSubtitlesCommand() =>
        new("subtitles", "Commands for working with subtitle files")
        {
            this.CreateSubtitlesGenerateContentCommand(),
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
}
