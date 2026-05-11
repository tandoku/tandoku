namespace Tandoku.CommandLine;

using System.CommandLine;
using System.IO.Abstractions;
using Tandoku.Volume;

public sealed partial class Program
{
    private Command CreateVolumeCommand() =>
        new("volume", "Commands for working with tandoku volumes")
        {
            this.CreateVolumeInitCommand(),
            this.CreateVolumeNewCommand(),
            this.CreateVolumeInfoCommand(),
            this.CreateVolumeSetCommand(),
            this.CreateVolumeRenameCommand(),
            this.CreateVolumeListCommand(),
        };

    private Command CreateVolumeInitCommand()
    {
        var pathArgument = new Argument<DirectoryInfo?>("path")
        {
            Description = "Directory for new tandoku volume"
        }.AcceptLegalFilePathsOnly();
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Allow initialization in non-empty directory"
        };

        var command = new Command("init", "Initializes a new tandoku volume in the current or specified directory")
        {
            pathArgument,
            forceOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var directory = parseResult.GetValue(pathArgument);
            var force = parseResult.GetValue(forceOption);
            var jsonOutput = parseResult.GetValue(this.jsonOutputOption);

            var volumeManager = this.CreateVolumeManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var info = await volumeManager.InitializeAsync(path, force);
            if (jsonOutput)
            {
                // TODO: use VolumeInfo directly, or copy to a JSON serializable object?
                this.output.WriteJsonOutput(info);
            }
            else
            {
                this.output.WriteLine(@$"Initialized tandoku volume at {info.Path}");
            }
        });

        return command;
    }

    private Command CreateVolumeNewCommand()
    {
        var titleArgument = new Argument<string>("title")
        {
            Description = "Title of new tandoku volume",
            Arity = ArgumentArity.ExactlyOne,
        };
        var pathOption = new Option<DirectoryInfo?>("--path", "-p")
        {
            Description = "Containing directory for new tandoku volume",
        }.AcceptLegalFilePathsOnly();
        var monikerOption = new Option<string?>("--moniker", "-m")
        {
            Description = "Optional moniker to identify volume, prepended to volume directory",
        };
        var tagsOption = new Option<string>("--tags", "-t")
        {
            Description = "Optional comma-separated tags for volume",
        };
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Allow new volume in non-empty directory",
        };

        var command = new Command("new", "Creates a new tandoku volume under the current or specified directory")
        {
            titleArgument,
            pathOption,
            monikerOption,
            tagsOption,
            forceOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var title = parseResult.GetRequiredValue(titleArgument);
            var (directory, moniker, tags, force) = parseResult.GetValues(
                pathOption,
                monikerOption,
                tagsOption,
                forceOption);

            var volumeManager = this.CreateVolumeManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var tagsArray = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var info = await volumeManager.CreateNewAsync(path, title, moniker, tagsArray, force);
            this.output.WriteLine(@$"Created new tandoku volume ""{title}"" at {info.Path}");
        });

        return command;
    }

    private Command CreateVolumeInfoCommand()
    {
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("info", "Displays information about the current or specified volume")
        {
            volumeBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var volumeDirectory = parseResult.GetValue(volumeBinder);
            var jsonOutput = parseResult.GetValue(this.jsonOutputOption);

            var volumeManager = this.CreateVolumeManager();
            var info = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            if (jsonOutput)
            {
                // TODO: use VolumeInfo directly, or copy to a JSON serializable object?
                this.output.WriteJsonOutput(info);
            }
            else
            {
                this.output.WriteLine($"Path: {info.Path}");
                this.output.WriteLine($"Version: {info.Version}");
                this.output.WriteLine($"Definition path: {info.DefinitionPath}");
                this.output.WriteLine($"Title: {info.Definition.Title}");
                this.output.WriteLine($"Moniker: {info.Definition.Moniker.ToOutputString()}");
                this.output.WriteLine($"Language: {info.Definition.Language}");
                //this.output.WriteLine($"Reference language: {info.Definition.ReferenceLanguage.ToOutputString()}");
                this.output.WriteLine($"Tags: {info.Definition.Tags.ToOutputString()}");
                this.output.WriteLine($"Workflow: {info.Definition.Workflow}");
            }
        });

        return command;
    }

    private Command CreateVolumeSetCommand()
    {
        var propertyArgument = new Argument<string>("property")
        {
            Description = "Name of definition property to set",
            Arity = ArgumentArity.ExactlyOne,
        };
        var valueArgument = new Argument<string>("value")
        {
            Description = "Value of definition property to set",
            Arity = ArgumentArity.ExactlyOne,
        };
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("set", "Sets a definition property for the current or specified volume")
        {
            propertyArgument,
            valueArgument,
            volumeBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var property = parseResult.GetRequiredValue(propertyArgument);
            var value = parseResult.GetRequiredValue(valueArgument);
            var volumeDirectory = parseResult.GetValue(volumeBinder);

            var volumeManager = this.CreateVolumeManager();
            var info = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            var modifiedDefinition = property switch
            {
                "title" => info.Definition with { Title = value },
                "workflow" => info.Definition with { Workflow = value },
                _ => throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected property name."),
            };
            await volumeManager.SetDefinitionAsync(volumeDirectory.FullName, modifiedDefinition);
        });

        return command;
    }

    private Command CreateVolumeRenameCommand()
    {
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("rename", "Renames the current or specified volume to match definition metadata")
        {
            volumeBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var volumeDirectory = parseResult.GetValue(volumeBinder);
            var jsonOutput = parseResult.GetValue(this.jsonOutputOption);

            var volumeManager = this.CreateVolumeManager();
            var result = await volumeManager.RenameVolumeDirectory(volumeDirectory.FullName);
            if (jsonOutput)
            {
                // TODO: use result directly, or copy to a JSON serializable object?
                this.output.WriteJsonOutput(result);
            }
            else
            {
                this.output.WriteLine($"Renamed {result.OriginalPath} to {result.RenamedPath}");
            }
        });

        return command;
    }

    private Command CreateVolumeListCommand()
    {
        var pathArgument = new Argument<DirectoryInfo?>("path")
        {
            Description = "Directory to search for tandoku volumes",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptLegalFilePathsOnly();

        var allOption = new Option<bool>("--all", "-a")
        {
            Description = "Return all volumes in the current or specified library"
        };

        var command = new Command("list", "Lists volumes in the current or specified directory")
        {
            pathArgument,
            allOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var directory = parseResult.GetValue(pathArgument);
            var all = parseResult.GetValue(allOption);

            var volumeManager = this.CreateVolumeManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var expandScope = all ? ExpandedScope.ParentLibrary : ExpandedScope.ParentVolume;
            foreach (var volumePath in volumeManager.GetVolumeDirectories(path, expandScope))
            {
                var volumeInfo = await volumeManager.GetInfoAsync(volumePath);
                this.output.WriteLine($"{volumeInfo.Definition.Title}\t{volumeInfo.Path}");
            }
        });

        return command;
    }

    private VolumeManager CreateVolumeManager() => new(this.fileSystem);

    private VolumeBinder CreateVolumeBinder() => new(this.fileSystem, this.CreateVolumeManager);

    // TODO: change this to return a VolumeContext (or maybe VolumeLocation) wrapper object rather than IDirectoryInfo
    // (VolumeManager APIs should all accept this instead)
    private sealed class VolumeBinder(IFileSystem fileSystem, Func<VolumeManager> createVolumeManager) : ICommandBinder<IDirectoryInfo>
    {
        private readonly Option<DirectoryInfo?> volumeOption = new Option<DirectoryInfo?>("--volume", "-v")
        {
            Description = "Volume directory path",
        }.AcceptLegalFilePathsOnly();

        public void AddToCommand(Command command) => command.Options.Add(this.volumeOption);

        public IDirectoryInfo Resolve(ParseResult parseResult)
        {
            var directoryInfo = parseResult.GetValue(this.volumeOption);

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
