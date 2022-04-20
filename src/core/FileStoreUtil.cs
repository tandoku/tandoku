namespace Tandoku;

internal static class FileStoreUtil
{
    internal static IEnumerable<FileSystemInfo> ExpandPaths(IEnumerable<FileSystemInfo> inputPaths)
    {
        foreach (var path in inputPaths)
        {
            if (path is FileInfo fileInfo)
            {
                if (fileInfo.DirectoryName != null)
                {
                    foreach (var childPath in Directory.EnumerateFiles(fileInfo.DirectoryName, fileInfo.Name))
                        yield return new FileInfo(childPath);
                }
                else
                {
                    yield return fileInfo;
                }
            }

            if (path is DirectoryInfo dirInfo)
            {
                // TODO: return all files?
            }
        }
    }
}
