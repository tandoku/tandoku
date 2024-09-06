namespace Tandoku.Subtitles;

using System.IO.Abstractions;

internal static class SubtitleExtensions
{
    private static readonly IReadOnlyList<string> knownSubtitleExtensions = [".ass", ".srt", ".vtt"];

    internal static IEnumerable<IFileInfo> EnumerateSubtitleFiles(this IDirectoryInfo directory)
    {
        foreach (var extension in knownSubtitleExtensions)
        {
            foreach (var file in directory.EnumerateFiles($"*{extension}"))
                yield return file;
        }
    }
}
