namespace Tandoku.Content;

using System.IO.Abstractions;

internal static class ContentExtensions
{
    internal static IEnumerable<IFileInfo> EnumerateContentFiles(this IDirectoryInfo directory)
    {
        // TODO - remove this in favor of <slug>.content.yaml for single content file
        foreach (var file in directory.EnumerateFiles("content.yaml"))
            yield return file;

        foreach (var file in directory.EnumerateFiles("*.content.yaml"))
            yield return file;
    }
}
