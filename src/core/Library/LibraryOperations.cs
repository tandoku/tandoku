namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryOperations
{
    private readonly IFileSystem _fileSystem;

    public LibraryOperations() : this(new FileSystem()) { }

    public LibraryOperations(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public Task<LibraryInfo> InitializeAsync(DirectoryInfo? pathInfo) =>
        InitializeAsync(_fileSystem.DirectoryInfo.Wrap(pathInfo));

    public async Task<LibraryInfo> InitializeAsync(IDirectoryInfo? pathInfo)
    {
        if (pathInfo == null)
        {
            pathInfo = _fileSystem.DirectoryInfo.New(
                _fileSystem.Directory.GetCurrentDirectory());
        }
        
        if (!pathInfo.Exists)
            pathInfo.Create();

        string metadataPath = _fileSystem.Path.Combine(
            pathInfo.FullName,
            "library.tdkl.yaml");

        await _fileSystem.File.WriteAllTextAsync(metadataPath, "language: ja");

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
