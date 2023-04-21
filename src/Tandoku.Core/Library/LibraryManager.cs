namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryManager
{
    private const string LibraryMetadataDirectory = ".tandoku-library";
    private const string LibraryVersionFileName = "version";
    private const string LibraryDefinitionFileName = "library.yaml";
    private const string DefaultLanguage = "ja";
    private const string DefaultReferenceLanguage = "en";

    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<LibraryInfo> InitializeAsync(string path, bool force = false)
    {
        var directory = this.fileSystem.GetDirectory(path);
        if (directory.Exists)
        {
            if (!force && directory.EnumerateFileSystemInfos().Any())
                throw new ArgumentException("The specified directory is not empty and force is not specified.");
        }
        else
        {
            // Note: this can throw IOException if a conflicting file exists at the path
            directory.Create();
        }

        var metadataDirectory = directory.GetSubdirectory(LibraryMetadataDirectory);
        if (metadataDirectory.Exists)
        {
            throw new InvalidOperationException("A tandoku library already exists in the specified directory.");
        }
        else
        {
            metadataDirectory.Create();
        }

        var versionFile = metadataDirectory.GetFile(LibraryVersionFileName);
        var version = LibraryVersion.Latest;
        await version.WriteToAsync(versionFile);

        var definitionFile = directory.GetFile(LibraryDefinitionFileName);
        var definition = new LibraryDefinition
        {
            Language = DefaultLanguage,
            ReferenceLanguage = DefaultReferenceLanguage,
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
}
