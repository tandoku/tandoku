namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryManager
{
    private readonly IFileSystem fileSystem;

    public LibraryManager() : this(new FileSystem()) { }

    public LibraryManager(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public Task<LibraryInfo> InitializeAsync(string? path)
    {
        var pathInfo = this.fileSystem.DirectoryInfo.New(
            !string.IsNullOrEmpty(path) ? path : this.fileSystem.Directory.GetCurrentDirectory());
        
        return this.InitializeAsync(pathInfo);
    }

    private async Task<LibraryInfo> InitializeAsync(IDirectoryInfo pathInfo)
    {
        if (!pathInfo.Exists)
            pathInfo.Create();

        // TODO: add more arguments and properly implement this

        var metadataPath = this.fileSystem.Path.Combine(
            pathInfo.FullName,
            "library.tdkl.yaml");

        await this.fileSystem.File.WriteAllTextAsync(metadataPath, "language: ja");

        return new LibraryInfo(pathInfo.FullName, metadataPath);
    }

    public LibraryInfo GetInfo(FileSystemInfo? path)
    {
        if (path != null)
        {
        }
        else
        {
        }
        throw new NotImplementedException();
    }
}
