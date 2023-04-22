namespace Tandoku.Library;

using System.IO.Abstractions;
using Tandoku.Packaging;

public sealed class LibraryManager
{
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
        var directory = this.fileSystem.GetDirectory(path);
        return CreatePackager().ResolvePackageDirectoryPath(directory, checkAncestors);
    }

    private static Packager<LibraryVersion> CreatePackager() => new("library");
}
