namespace Tandoku.Library;

using Spectre.IO;

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
        this.fileSystem = fileSystem ?? FileSystem.Shared;
    }

    public Task<LibraryInfo> InitializeAsync(string path, bool force = false)
    {
        var directory = this.fileSystem.GetDirectory(path);
        return this.InitializeAsync(directory, force);
    }

    private async Task<LibraryInfo> InitializeAsync(IDirectory directory, bool force)
    {
        if (directory.Exists)
        {
            if (!force && (directory.GetFiles("*", SearchScope.Current).Any() ||
                    directory.GetDirectories("*", SearchScope.Current).Any()))
            {
                throw new ArgumentException("The specified directory is not empty and force is not specified.");
            }
        }
        else
        {
            // Note: this can throw IOException if a conflicting file exists at the path
            directory.Create();
        }

        var definition = new LibraryDefinition
        {
            Language = DefaultLanguage,
            ReferenceLanguage = DefaultReferenceLanguage,
        };

        var definitionPath = directory.Path.CombineWithFilePath(LibraryDefinitionFileName);

        using var definitionWriter = new StreamWriter(this.fileSystem.File.OpenWrite(definitionPath));
        await definition.WriteYamlAsync(definitionWriter);

        return new LibraryInfo(directory.Path.FullPath, definitionPath.FullPath, definition);
    }

    public async Task<LibraryInfo> GetInfoAsync(string definitionPath)
    {
        var definitionFile = this.fileSystem.GetFile(definitionPath);

        using var definitionReader = new StreamReader(definitionFile.OpenRead());
        var definition = await LibraryDefinition.ReadYamlAsync(definitionReader);

        return new LibraryInfo(
            definitionFile.Path.GetDirectory().FullPath,
            definitionFile.Path.FullPath,
            definition);
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
            var libraryDefinitions = directory.GetFiles(LibraryDefinitionSearchPattern, SearchScope.Current).ToList();
            return libraryDefinitions.Count switch
            {
                0 => checkAncestors && directory.Path.GetParent() is var parent && parent is not null ?
                    this.ResolveLibraryDefinitionPath(parent.FullPath, checkAncestors) :
                    null,
                1 => libraryDefinitions[0].Path.FullPath,
                _ => throw new ArgumentException("The specified path contains more than one tandoku library definition."),
            };
        }
        else
        {
            throw new ArgumentException("The specified path does not exist.");
        }
    }
}
