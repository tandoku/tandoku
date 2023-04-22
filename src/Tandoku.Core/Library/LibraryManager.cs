namespace Tandoku.Library;

using System.IO.Abstractions;
using Tandoku.Packaging;

public sealed class LibraryManager
{
    private const string LibraryMetadataDirectory = ".tandoku-library";
    private const string LibraryDefinitionFileName = "library.yaml";

    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<LibraryInfo> InitializeAsync(string path, bool force = false)
    {
        var packager = CreatePackager();
        var directory = this.fileSystem.GetDirectory(path);
        var version = LibraryVersion.Latest;
        await packager.InitializePackageAsync(directory, version, force);

        var definition = new LibraryDefinition
        {
            Language = LanguageConstants.DefaultLanguage,
            //ReferenceLanguage = LanguageConstants.DefaultReferenceLanguage,
        };
        var definitionFile = await packager.WritePackagePart(directory, LibraryDefinitionFileName, definition);

        return new LibraryInfo(directory.FullName, version, definitionFile.FullName, definition);
    }

    public async Task<LibraryInfo> GetInfoAsync(string libraryPath)
    {
        var packager = CreatePackager();
        var libraryDirectory = this.fileSystem.GetDirectory(libraryPath);
        var version = await packager.GetPackageMetadataAsync(libraryDirectory);

        var (definitionFile, definition) = await packager.ReadPackagePart<LibraryDefinition>(
            libraryDirectory,
            LibraryDefinitionFileName,
            "definition");

        return new LibraryInfo(libraryDirectory.FullName, version, definitionFile.FullName, definition);
    }

    public string? ResolveLibraryDirectoryPath(string path, bool checkAncestors = false)
    {
        if (this.fileSystem.Directory.Exists(path))
        {
            var directory = this.fileSystem.GetDirectory(path);
            var metadataDirectory = directory.GetSubdirectory(LibraryMetadataDirectory);
            if (metadataDirectory.Exists)
                return directory.FullName;

            return checkAncestors && directory.Parent is not null ?
                this.ResolveLibraryDirectoryPath(directory.Parent.FullName, checkAncestors) :
                null;
        }
        else if (this.fileSystem.File.Exists(path))
        {
            throw new ArgumentOutOfRangeException(nameof(path), "The specified path refers to a file where a directory is expected.");
        }
        else
        {
            throw new ArgumentException("The specified path does not exist.");
        }
    }

    private static Packager<LibraryVersion> CreatePackager() => new("library");
}
