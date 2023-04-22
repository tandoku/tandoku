namespace Tandoku;

using System.IO.Abstractions;

public static class FileSystemExtensions
{
    public static IDirectoryInfo GetDirectory(this IFileSystem fileSystem, string path) =>
        fileSystem.DirectoryInfo.New(path);
    public static IFileInfo GetFile(this IFileSystem fileSystem, string path) =>
        fileSystem.FileInfo.New(path);

    public static string GetPath(this IDirectoryInfo directory, string path) =>
        directory.FileSystem.Path.Join(directory.FullName, path);
    public static IDirectoryInfo GetSubdirectory(this IDirectoryInfo directory, string path) =>
        GetDirectory(directory.FileSystem, GetPath(directory, path));
    public static IFileInfo GetFile(this IDirectoryInfo directory, string path) =>
        GetFile(directory.FileSystem, GetPath(directory, path));

    internal static string CleanInvalidFileNameChars(this IFileSystem fileSystem, string s, string? replaceWith = "_") =>
        string.Join(replaceWith, s.Split(fileSystem.Path.GetInvalidFileNameChars())).Trim();
}
