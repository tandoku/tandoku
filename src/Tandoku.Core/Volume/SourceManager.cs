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
    public Task<IReadOnlyList<string>> ImportFilesAsync(IEnumerable<string> paths)
    {
        var sourceDirectory = this.GetSourceDirectory(createIfNotExists: true);

        var importedPaths = new List<string>();

        // TODO: make this more transactional by checking for errors upfront and doing all actual copy operations last
        foreach (var path in paths)
        {
            var originFile = this.fileSystem.GetFile(path);
            var targetPath = sourceDirectory.GetPath(originFile.Name);
            var targetFile = originFile.CopyTo(targetPath);
            importedPaths.Add(targetFile.FullName);
        }
        return Task.FromResult<IReadOnlyList<string>>(importedPaths);
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
