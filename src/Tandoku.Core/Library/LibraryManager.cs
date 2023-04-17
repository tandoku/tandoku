namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryManager
{
    private const string LibraryDefinitionFileExtension = ".tdkl.yaml";
    private const string LibraryDefinitionFileName = "library" + LibraryDefinitionFileExtension;
    private const string LibraryDefinitionSearchPattern = "*" + LibraryDefinitionFileExtension;
    private const string DefaultLanguage = "ja";
    private const string DefaultReferenceLanguage = "en";

    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public Task<LibraryInfo> InitializeAsync(string path, bool force = false) =>
        this.InitializeAsync(this.fileSystem.GetDirectory(path), force);

    private async Task<LibraryInfo> InitializeAsync(IDirectoryInfo directory, bool force)
    {
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

        var definitionFile = directory.GetFile(LibraryDefinitionFileName);
        var definition = new LibraryDefinition
        {
            Language = DefaultLanguage,
            ReferenceLanguage = DefaultReferenceLanguage,
        };
        await definition.WriteYamlAsync(definitionFile);

        return new LibraryInfo(directory.FullName, definitionFile.FullName, definition);
    }

    public async Task<LibraryInfo> GetInfoAsync(string definitionPath)
    {
        var definitionFile = this.fileSystem.GetFile(definitionPath);
        if (definitionFile.DirectoryName is null)
            throw new ArgumentOutOfRangeException(nameof(definitionPath));

        var definition = await LibraryDefinition.ReadYamlAsync(definitionFile);

        return new LibraryInfo(definitionFile.DirectoryName, definitionFile.FullName, definition);
    }
    public string? ResolveLibraryDefinitionPath(string path, bool checkAncestors = false)
    {
        if (this.fileSystem.File.Exists(path))
        {
            return path;
        }
        else if (this.fileSystem.Directory.Exists(path))
        {
            var directory = this.fileSystem.GetDirectory(path);
            var libraryDefinitions = directory.GetFiles(LibraryDefinitionSearchPattern);
            return libraryDefinitions.Length switch
            {
                0 => checkAncestors && directory.Parent is not null ?
                    this.ResolveLibraryDefinitionPath(directory.Parent.FullName, checkAncestors) :
                    null,
                1 => libraryDefinitions[0].FullName,
                _ => throw new ArgumentException("The specified path contains more than one tandoku library definition."),
            }; ;
        }
        else
        {
            throw new ArgumentException("The specified path does not exist.");
        }
    }
}
