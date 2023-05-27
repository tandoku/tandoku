namespace Tandoku.Volume;

using System.IO.Abstractions;

public sealed class SourceManager
{
    private readonly VolumeInfo volumeInfo;
    private readonly IFileSystem fileSystem;

    public SourceManager(VolumeInfo volumeInfo, IFileSystem? fileSystem = null)
    {
        this.volumeInfo = volumeInfo;
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    // TODO:
    // - language (Auto, None, or language code)
    // - folder (unused, investigate, original)
    // - optional target filename (rename incoming files from Calibre)
    public Task<IReadOnlyList<string>> ImportFilesAsync(IEnumerable<string> paths, string? targetFileName = null)
    {
        var sourceDirectory = this.GetSourceDirectory(createIfNotExists: true);

        var copyOperations = new List<(IFileInfo Source, string Target)>();
        foreach (var path in paths)
        {
            var originFile = this.fileSystem.GetFile(path);
            var targetPath = sourceDirectory.GetPath(targetFileName ?? originFile.Name);
            copyOperations.Add((originFile, targetPath));
        }

        if (copyOperations.DistinctBy(o => o.Target, this.fileSystem.Path.GetComparer()).Count() < copyOperations.Count)
            throw new ArgumentException("Target filenames must be unique.");

        foreach (var copyOperation in copyOperations)
            copyOperation.Source.CopyTo(copyOperation.Target);

        return Task.FromResult<IReadOnlyList<string>>(copyOperations.Select(o => o.Target).ToArray());
    }

    private IDirectoryInfo GetSourceDirectory(bool createIfNotExists = false)
    {
        // TODO: use layout instead of hard-coded paths
        var directory = this.fileSystem.GetDirectory(
            this.fileSystem.Path.Join(this.volumeInfo.Path, "source"));

        if (createIfNotExists && !directory.Exists)
            directory.Create();

        return directory;
    }
}
