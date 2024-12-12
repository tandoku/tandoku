namespace Tandoku;

using System.IO.Abstractions;

public static class FileSystemExtensions
{
    public static IDirectoryInfo GetDirectory(this IFileSystem fileSystem, string path) =>
        fileSystem.DirectoryInfo.New(path);
    public static IDirectoryInfo GetCurrentDirectory(this IFileSystem fileSystem) =>
        GetDirectory(fileSystem, fileSystem.Directory.GetCurrentDirectory());
    public static IFileInfo GetFile(this IFileSystem fileSystem, string path) =>
        fileSystem.FileInfo.New(path);
    public static IEnumerable<IFileInfo> EnumerateFiles(this IFileSystem fileSystem, string path)
    {
        var directory = GetDirectory(fileSystem, path);
        if (directory.Exists)
            return directory.EnumerateFiles();

        directory = fileSystem.Directory.GetParent(path);
        return directory?.EnumerateFiles(fileSystem.Path.GetFileName(path)) ?? [];
    }

    public static string GetPath(this IDirectoryInfo directory, string path) =>
        directory.FileSystem.Path.Join(directory.FullName, path);
    public static IDirectoryInfo GetSubdirectory(this IDirectoryInfo directory, string path) =>
        GetDirectory(directory.FileSystem, GetPath(directory, path));
    public static IFileInfo GetFile(this IDirectoryInfo directory, string path) =>
        GetFile(directory.FileSystem, GetPath(directory, path));

    public static string? GetBaseName(this IFileInfo file) =>
        GetBaseName(file.FileSystem.Path, file.Name);

    public static string? GetBaseName(this IPath path, string fileName)
    {
        var finalExtension = path.GetExtension(fileName).ToLowerInvariant();
        switch (finalExtension)
        {
            case ".yaml":
                var baseName = path.GetFileNameWithoutExtension(fileName);
                var secondaryExtension = path.GetExtension(baseName).ToLowerInvariant();
                switch (secondaryExtension)
                {
                    case ".content":
                        return path.GetFileNameWithoutExtension(baseName);
                }
                break;
        }
        return null;
    }

    // Note: currently using OrdinalIgnoreCase always, may need to make this OS-dependent
    // (but would also like to avoid creating file systems that are not portable across operating systems)
    public static StringComparer GetComparer(this IPath path) => StringComparer.OrdinalIgnoreCase;

    internal static string CleanInvalidFileNameChars(this IFileSystem fileSystem, string s, string? replaceWith = "_") =>
        string.Join(replaceWith, s.Split(fileSystem.Path.GetInvalidFileNameChars())).Trim();
}
