namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryManager
{
    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public Task<LibraryInfo> InitializeAsync(string? path, bool force = false)
    {
        var pathInfo = this.fileSystem.DirectoryInfo.New(
            !string.IsNullOrEmpty(path) ? path : this.fileSystem.Directory.GetCurrentDirectory());
        
        return this.InitializeAsync(pathInfo, force);
    }

    private async Task<LibraryInfo> InitializeAsync(IDirectoryInfo pathInfo, bool force)
    {
        if (pathInfo.Exists)
        {
            if (!force && pathInfo.EnumerateFileSystemInfos().Any())
                throw new ArgumentException("The specified directory is not empty and force is not specified.");
        }
        else
        {
            pathInfo.Create();
        }

        // TODO: add more arguments and properly implement this

        var metadataPath = this.fileSystem.Path.Join(
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
