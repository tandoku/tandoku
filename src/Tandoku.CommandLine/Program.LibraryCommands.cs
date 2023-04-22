namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;
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
        var pathArgument = new Argument<DirectoryInfo?>("path", "Directory for new tandoku library")
        {
            Arity = ArgumentArity.ZeroOrOne,
        }.LegalFilePathsOnly();
        var forceOption = new Option<bool>(new[] { "--force", "-f" }, "Allow new library in non-empty directory");

        var command = new Command("init", "Initializes a new tandoku library in the current or specified directory")
        {
            pathArgument,
            forceOption,
        };

        command.SetHandler(async (directory, force) =>
        {
            var libraryManager = this.CreateLibraryManager();
            var path = directory?.FullName ?? this.fileSystem.Directory.GetCurrentDirectory();
            var info = await libraryManager.InitializeAsync(path, force);
            this.console.WriteLine($"Initialized new tandoku library at {info.Path}");
        }, pathArgument, forceOption);

        return command;
    }

    private Command CreateLibraryInfoCommand()
    {
        var libraryBinder = new LibraryBinder(this.fileSystem, this.environment, this.CreateLibraryManager);

        var command = new Command("info", "Displays information about the current or specified library")
        {
            libraryBinder.LibraryOption,
        };

        command.SetHandler(async (libraryDirectory) =>
        {
            var libraryManager = this.CreateLibraryManager();
            var info = await libraryManager.GetInfoAsync(libraryDirectory.FullName);
            this.console.WriteLine($"Path: {info.Path}");
            this.console.WriteLine($"Version: {info.Version}");
            this.console.WriteLine($"Definition path: {info.DefinitionPath}");
            this.console.WriteLine($"Language: {info.Definition.Language}");
            //this.console.WriteLine($"Reference language: {info.Definition.ReferenceLanguage.ToOutputString()}");
        }, libraryBinder);

        return command;
    }

    private LibraryManager CreateLibraryManager() => new(this.fileSystem);

    private sealed class LibraryBinder : BinderBase<IDirectoryInfo>
    {
        private readonly IFileSystem fileSystem;
        private readonly IEnvironment environment;
        private readonly Func<LibraryManager> createLibraryManager;

        internal LibraryBinder(IFileSystem fileSystem, IEnvironment environment, Func<LibraryManager> createLibraryManager)
        {
            this.fileSystem = fileSystem;
            this.environment = environment;
            this.createLibraryManager = createLibraryManager;

            this.LibraryOption = new Option<DirectoryInfo?>(
                new[] { "--library", "-l" },
                "Library directory path")
                .LegalFilePathsOnly();
        }

        internal Option<DirectoryInfo?> LibraryOption { get; }

        protected override IDirectoryInfo GetBoundValue(BindingContext bindingContext)
        {
            var directoryInfo = bindingContext.ParseResult.GetValueForOption(this.LibraryOption);

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
