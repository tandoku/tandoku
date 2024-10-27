namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;
using System.IO.Abstractions;
using Spectre.Console;
using Tandoku.Volume;

public sealed partial class Program
{
    private Command CreateVolumeCommand() =>
        new("volume", "Commands for working with tandoku volumes")
        {
            this.CreateVolumeNewCommand(),
            this.CreateVolumeInfoCommand(),
            this.CreateVolumeSetCommand(),
            this.CreateVolumeRenameCommand(),
            this.CreateVolumeListCommand(),
        };

    private Command CreateVolumeNewCommand()
    {
        var titleArgument = new Argument<string>("title", "Title of new tandoku volume");
        var pathOption = new Option<DirectoryInfo?>(["--path", "-p"], "Containing directory for new tandoku volume")
            .LegalFilePathsOnly();
        var monikerOption = new Option<string?>(["--moniker", "-m"], "Optional moniker to identify volume, prepended to volume directory");
        var tagsOption = new Option<string>(["--tags", "-t"], "Optional comma-separated tags for volume");
        var forceOption = new Option<bool>(["--force", "-f"], "Allow new volume in non-empty directory");

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
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("info", "Displays information about the current or specified volume")
        {
            volumeBinder,
        };

        command.SetHandler(async (volumeDirectory, jsonOutput) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var info = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            if (jsonOutput)
            {
                // TODO: use VolumeInfo directly, or copy to a JSON serializable object?
                //       Promote Version property or add JsonConverter for VolumeVersion (probably can't inherit from interface?)
                this.console.WriteJsonOutput(info);
            }
            else
            {
                this.console.WriteLine($"Path: {info.Path}");
                this.console.WriteLine($"Version: {info.Version}");
                this.console.WriteLine($"Definition path: {info.DefinitionPath}");
                this.console.WriteLine($"Title: {info.Definition.Title}");
                this.console.WriteLine($"Moniker: {info.Definition.Moniker.ToOutputString()}");
                this.console.WriteLine($"Language: {info.Definition.Language}");
                //this.console.WriteLine($"Reference language: {info.Definition.ReferenceLanguage.ToOutputString()}");
                this.console.WriteLine($"Tags: {info.Definition.Tags.ToOutputString()}");
            }
        }, volumeBinder, this.jsonOutputOption);

        return command;
    }

    private Command CreateVolumeSetCommand()
    {
        var propertyArgument = new Argument<string>("property", "Name of definition property to set")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
        var valueArgument = new Argument<string>("value", "Value of definition property to set")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("set", "Sets a definition property for the current or specified volume")
        {
            propertyArgument,
            valueArgument,
            volumeBinder,
        };

        command.SetHandler(async (property, value, volumeDirectory) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var info = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            var modifiedDefinition = property switch
            {
                "title" => info.Definition with { Title = value },
                _ => throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected property name."),
            };
            await volumeManager.SetDefinitionAsync(volumeDirectory.FullName, modifiedDefinition);
        }, propertyArgument, valueArgument, volumeBinder);

        return command;
    }

    private Command CreateVolumeRenameCommand()
    {
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("rename", "Renames the current or specified volume to match definition metadata")
        {
            volumeBinder,
        };

        command.SetHandler(async (volumeDirectory, jsonOutput) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var result = await volumeManager.RenameVolumeDirectory(volumeDirectory.FullName);
            if (jsonOutput)
            {
                // TODO: use result directly, or copy to a JSON serializable object?
                this.console.WriteJsonOutput(result);
            }
            else
            {
                this.console.WriteLine($"Renamed {result.OriginalPath} to {result.RenamedPath}");
            }
        }, volumeBinder, this.jsonOutputOption);

        return command;
    }

    private Command CreateVolumeListCommand()
    {
        var pathArgument = new Argument<DirectoryInfo?>("path", "Directory to search for tandoku volumes")
        {
            Arity = ArgumentArity.ZeroOrOne,
        }.LegalFilePathsOnly();

        var allOption = new Option<bool>(["--all", "-a"], "Return all volumes in the current or specified library");

        var command = new Command("list", "Lists volumes in the current or specified directory")
        {
            pathArgument,
            allOption,
        };

        command.SetHandler(async (directory, all) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var expandScope = all ? ExpandedScope.ParentLibrary : ExpandedScope.ParentVolume;
            foreach (var volumePath in volumeManager.GetVolumeDirectories(path, expandScope))
            {
                var volumeInfo = await volumeManager.GetInfoAsync(volumePath);
                this.console.WriteLine($"{volumeInfo.Definition.Title}\t{volumeInfo.Path}");
            }
        }, pathArgument, allOption);

        return command;
    }

    private VolumeManager CreateVolumeManager() => new(this.fileSystem);

    private VolumeBinder CreateVolumeBinder() => new(this.fileSystem, this.CreateVolumeManager);

    // TODO: change this to return a VolumeContext (or maybe VolumeLocation) wrapper object rather than IDirectoryInfo
    // (VolumeManager APIs should all accept this instead)
    private sealed class VolumeBinder(IFileSystem fileSystem, Func<VolumeManager> createVolumeManager) :
        BinderBase<IDirectoryInfo>, ICommandBinder
    {
        private readonly Option<DirectoryInfo?> volumeOption = new Option<DirectoryInfo?>(
            ["--volume", "-v"],
            "Volume directory path")
            .LegalFilePathsOnly();

        public void AddToCommand(Command command) => command.Add(this.volumeOption);

        protected override IDirectoryInfo GetBoundValue(BindingContext bindingContext)
        {
            var directoryInfo = bindingContext.ParseResult.GetValueForOption(this.volumeOption);

            var volumeManager = createVolumeManager();

            var volumeDirectoryPath = directoryInfo is not null ?
                volumeManager.ResolveVolumeDirectoryPath(directoryInfo.FullName) :
                volumeManager.ResolveVolumeDirectoryPath(fileSystem.Directory.GetCurrentDirectory(), checkAncestors: true);

            return volumeDirectoryPath is not null ?
                fileSystem.GetDirectory(volumeDirectoryPath) :
                throw new ArgumentException("The specified path does not contain a tandoku volume.");
        }
    }
}
