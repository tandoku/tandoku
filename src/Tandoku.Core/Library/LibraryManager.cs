namespace Tandoku.Library;

using System.IO.Abstractions;
using Tandoku.Packaging;

public sealed class LibraryManager
{
    private const string LibraryMetadataDirectory = ".tandoku-library";
    private const string LibraryVersionFileName = "version";
    private const string LibraryDefinitionFileName = "library.yaml";

    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<LibraryInfo> InitializeAsync(string path, bool force = false)
    {
        var directory = this.fileSystem.GetDirectory(path);
        var version = LibraryVersion.Latest;
        var packager = CreatePackager();
        await packager.InitializePackageAsync(directory, version, force);

        var definitionFile = directory.GetFile(LibraryDefinitionFileName);
        var definition = new LibraryDefinition
        {
            Language = LanguageConstants.DefaultLanguage,
            ReferenceLanguage = LanguageConstants.DefaultReferenceLanguage,
        };
        await definition.WriteYamlAsync(definitionFile);

        return new LibraryInfo(directory.FullName, version, definitionFile.FullName, definition);
    }

    public async Task<LibraryInfo> GetInfoAsync(string libraryPath)
    {
        var libraryDirectory = this.fileSystem.GetDirectory(libraryPath);
        var metadataDirectory = libraryDirectory.GetSubdirectory(LibraryMetadataDirectory);

        var versionFile = metadataDirectory.GetFile(LibraryVersionFileName);
        var version = versionFile.Exists ?
            await LibraryVersion.ReadFromAsync(versionFile) :
            throw new ArgumentException("The specified directory is not a valid tandoku library");

        var definitionFile = libraryDirectory.GetFile(LibraryDefinitionFileName);
        var definition = definitionFile.Exists ?
            await LibraryDefinition.ReadYamlAsync(definitionFile) :
            throw new ArgumentException("The specified directory does not contain a tandoku library definition");

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
