namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;
using System.IO.Abstractions;
using Tandoku.Volume;

public sealed partial class Program
{
    private Command CreateVolumeCommand() =>
        new("volume", "Commands for working with tandoku volumes")
        {
            this.CreateVolumeNewCommand(),
            this.CreateVolumeInfoCommand(),
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

    private Command CreateVolumeInfoCommand()
    {
        var volumeBinder = new VolumeBinder(this.fileSystem, this.CreateVolumeManager);

        var command = new Command("info", "Displays information about the current or specified volume")
        {
            volumeBinder.VolumeOption,
        };

        command.SetHandler(async (volumeDirectory) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var info = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            this.console.WriteLine($"Path: {info.Path}");
            this.console.WriteLine($"Version: {info.Version}");
            this.console.WriteLine($"Definition path: {info.DefinitionPath}");
            this.console.WriteLine($"Title: {info.Definition.Title}");
            this.console.WriteLine($"Moniker: {info.Definition.Moniker.ToOutputString()}");
            this.console.WriteLine($"Language: {info.Definition.Language}");
            //this.console.WriteLine($"Reference language: {info.Definition.ReferenceLanguage.ToOutputString()}");
            this.console.WriteLine($"Tags: {info.Definition.Tags.ToOutputString()}");
        }, volumeBinder);

        return command;
    }

    private VolumeManager CreateVolumeManager() => new(this.fileSystem);

    private sealed class VolumeBinder : BinderBase<IDirectoryInfo>
    {
        private readonly IFileSystem fileSystem;
        private readonly Func<VolumeManager> createVolumeManager;

        internal VolumeBinder(IFileSystem fileSystem, Func<VolumeManager> createVolumeManager)
        {
            this.fileSystem = fileSystem;
            this.createVolumeManager = createVolumeManager;

            this.VolumeOption = new Option<DirectoryInfo?>(
                new[] { "--volume", "-v" },
                "Volume directory path")
                .LegalFilePathsOnly();
        }

        internal Option<DirectoryInfo?> VolumeOption { get; }

        protected override IDirectoryInfo GetBoundValue(BindingContext bindingContext)
        {
            var directoryInfo = bindingContext.ParseResult.GetValueForOption(this.VolumeOption);

            var volumeManager = this.createVolumeManager();

            var volumeDirectoryPath = directoryInfo is not null ?
                volumeManager.ResolveVolumeDirectoryPath(directoryInfo.FullName) :
                volumeManager.ResolveVolumeDirectoryPath(this.fileSystem.Directory.GetCurrentDirectory(), checkAncestors: true);

            return volumeDirectoryPath is not null ?
                this.fileSystem.GetDirectory(volumeDirectoryPath) :
                throw new ArgumentException("The specified path does not contain a tandoku volume.");
        }
    }
}
