namespace Tandoku.Content;

using System.IO.Abstractions;

internal static class ContentExtensions
{
    internal static IEnumerable<IFileInfo> EnumerateContentFiles(this IDirectoryInfo directory) =>
        directory.EnumerateFilesByExtension(".content.yaml");
}
