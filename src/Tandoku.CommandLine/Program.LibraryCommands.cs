namespace Tandoku.CommandLine;

using System.CommandLine;
using System.IO.Abstractions;
using Tandoku.CommandLine.Abstractions;
using Tandoku.Library;

public sealed partial class Program
{
    private Command CreateLibraryCommand() =>
        new("library", "Commands for working with tandoku libraries")
        {
            this.CreateLibraryInitCommand(),
            this.CreateLibraryInfoCommand(),
        };

    private Command CreateLibraryInitCommand()
    {
        var pathArgument = new Argument<DirectoryInfo?>("path")
        {
            Description = "Directory for new tandoku library",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptLegalFilePathsOnly();
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Allow new library in non-empty directory"
        };

        var command = new Command("init", "Initializes a new tandoku library in the current or specified directory")
        {
            pathArgument,
            forceOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var directory = parseResult.GetValue(pathArgument);
            var force = parseResult.GetValue(forceOption);

            var libraryManager = this.CreateLibraryManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var info = await libraryManager.InitializeAsync(path, force);
            this.output.WriteLine($"Initialized new tandoku library at {info.Path}");
        });

        return command;
    }

    private Command CreateLibraryInfoCommand()
    {
        var libraryBinder = new LibraryBinder(this.fileSystem, this.environment, this.CreateLibraryManager);

        var command = new Command("info", "Displays information about the current or specified library")
        {
            libraryBinder,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var libraryDirectory = parseResult.GetValue(libraryBinder);

            var libraryManager = this.CreateLibraryManager();
            var info = await libraryManager.GetInfoAsync(libraryDirectory.FullName);
            this.output.WriteLine($"Path: {info.Path}");
            this.output.WriteLine($"Version: {info.Version}");
            this.output.WriteLine($"Definition path: {info.DefinitionPath}");
            this.output.WriteLine($"Language: {info.Definition.Language}");
            //this.output.WriteLine($"Reference language: {info.Definition.ReferenceLanguage.ToOutputString()}");
        });

        return command;
    }

    private LibraryManager CreateLibraryManager() => new(this.fileSystem);

    private sealed class LibraryBinder : ICommandBinder<IDirectoryInfo>
    {
        private readonly IFileSystem fileSystem;
        private readonly IEnvironment environment;
        private readonly Func<LibraryManager> createLibraryManager;

        internal LibraryBinder(IFileSystem fileSystem, IEnvironment environment, Func<LibraryManager> createLibraryManager)
        {
            this.fileSystem = fileSystem;
            this.environment = environment;
            this.createLibraryManager = createLibraryManager;

            this.LibraryOption = new Option<DirectoryInfo?>("--library", "-l")
            {
                Description = "Library directory path",
            }.AcceptLegalFilePathsOnly();
        }

        private Option<DirectoryInfo?> LibraryOption { get; }

        public void AddToCommand(Command command) => command.Options.Add(this.LibraryOption);

        public IDirectoryInfo Resolve(ParseResult parseResult)
        {
            var directoryInfo = parseResult.GetValue(this.LibraryOption);

            var libraryManager = this.createLibraryManager();

            var libraryDirectoryPath = directoryInfo is not null ?
                libraryManager.ResolveLibraryDirectoryPath(directoryInfo.FullName) :
                libraryManager.ResolveLibraryDirectoryPath(this.fileSystem.Directory.GetCurrentDirectory(), checkAncestors: true);

            if (libraryDirectoryPath is null &&
                this.environment.GetEnvironmentVariable(KnownEnvironmentVariables.TandokuLibrary) is string envPath)
            {
                libraryDirectoryPath = libraryManager.ResolveLibraryDirectoryPath(envPath);
            }

            return libraryDirectoryPath is not null ?
                this.fileSystem.GetDirectory(libraryDirectoryPath) :
                throw new ArgumentException("The specified path does not contain a tandoku library.");
        }
    }
}
