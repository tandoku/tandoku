namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Volume;

public sealed partial class Program
{
    private Command CreateVolumeCommand() =>
        new("volume", "Commands for working with tandoku volumes")
        {
            this.CreateVolumeNewCommand(),
        };

    private Command CreateVolumeNewCommand()
    {
        var titleArgument = new Argument<string>("title", "Title of new tandoku volume");
        var pathOption = new Option<DirectoryInfo?>(new[] { "--path", "-p" }, "Containing directory for new tandoku volume")
            .LegalFilePathsOnly();
        var monikerOption = new Option<string?>(new[] { "--moniker", "-m" }, "Optional moniker to identify volume, prepended to volume directory");
        var tagsOption = new Option<string>(new[] { "--tags", "-t" }, "Optional comma-separated tags for volume");
        var forceOption = new Option<bool>(new[] { "--force", "-f" }, "Allow new volume in non-empty directory");

        var command = new Command("new", "Creates a new tandoku volume under the current or specified directory")
        {
            titleArgument,
            pathOption,
            monikerOption,
            tagsOption,
            forceOption,
        };

        command.SetHandler(async (title, directory, moniker, tags, force) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var tagsArray = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var info = await volumeManager.CreateNewAsync(title, path, moniker, tagsArray, force);
            this.console.WriteLine(@$"Created new tandoku volume ""{title}"" at {info.Path}");
        }, titleArgument, pathOption, monikerOption, tagsOption, forceOption);

        return command;
    }

    private VolumeManager CreateVolumeManager() => new(this.fileSystem);
}
