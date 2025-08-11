namespace Tandoku.Subtitles;

using System.IO.Abstractions;

internal static class SubtitleExtensions
{
    internal const string WebVtt = ".vtt";

    private static readonly IReadOnlyList<string> ttmlSubtitleExtensions = [".ttml", ".dfxp", ".xml"];
    private static readonly IReadOnlyList<string> knownSubtitleExtensions = [..ttmlSubtitleExtensions, WebVtt, ".ass", ".srt"];

    internal static IEnumerable<IFileInfo> EnumerateSubtitleFiles(this IDirectoryInfo directory) =>
        directory.EnumerateFilesByExtension(knownSubtitleExtensions);

    internal static IEnumerable<IFileInfo> EnumerateTtmlSubtitleFiles(this IDirectoryInfo directory) =>
        directory.EnumerateFilesByExtension(ttmlSubtitleExtensions);
}
