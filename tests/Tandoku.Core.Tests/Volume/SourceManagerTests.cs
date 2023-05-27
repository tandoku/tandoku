namespace Tandoku.Tests.Volume;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Volume;

public class SourceManagerTests
{
    private readonly MockFileSystem fileSystem;
    private readonly VolumeManager volumeManager;

    public SourceManagerTests()
    {
        this.fileSystem = new MockFileSystem();
        this.volumeManager = new VolumeManager(this.fileSystem);
    }

    [Fact]
    public async Task ImportFiles()
    {
        var externalFilePath = this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "external",
            "volume-source.txt");
        this.fileSystem.AddFile(externalFilePath, new MockFileData("source file content"));

        var (volumeInfo, sourceManager) = await this.Setup();
        var importedPaths = await sourceManager.ImportFilesAsync(new[] { externalFilePath });

        importedPaths.Should().BeEquivalentTo(
            this.fileSystem.Path.Join(volumeInfo.Path, "source", "volume-source.txt"));
        this.fileSystem.GetFile(importedPaths[0]).TextContents.Should().Be("source file content");
    }

    [Fact]
    public async Task ImportFiles_Multiple()
    {
        var externalDirectory = this.fileSystem.GetCurrentDirectory().GetSubdirectory("external");
        var externalFiles = Enumerable.Range(1, 3).Select(i => new
        {
            File = externalDirectory.GetFile($"volume-source-{i}.txt"),
            Data = new MockFileData($"source file content {i}"),
        });
        foreach (var externalFile in externalFiles)
            this.fileSystem.AddFile(externalFile.File.FullName, externalFile.Data);

        var (volumeInfo, sourceManager) = await this.Setup();
        var importedPaths = await sourceManager.ImportFilesAsync(externalFiles.Select(f => f.File.FullName));

        importedPaths.Should().BeEquivalentTo(
            externalFiles.Select(f => this.fileSystem.Path.Join(volumeInfo.Path, "source", f.File.Name)));
    }

    // TODO:
    //[Fact]
    //public async Task ImportFiles_TargetFileName()
    //{
    //}

    //[Fact]
    //public async Task ImportFiles_MultipleWithTargetFileName()
    //{
    //}

    private async Task<(VolumeInfo, SourceManager)> Setup(
        string title = "sample volume",
        string? containerPath = null,
        string? moniker = null,
        IEnumerable<string>? tags = null)
    {
        containerPath ??= this.fileSystem.Directory.GetCurrentDirectory();
        var volumeInfo = await this.volumeManager.CreateNewAsync(title, containerPath, moniker, tags);
        var sourceManager = new SourceManager(volumeInfo, this.fileSystem);
        return (volumeInfo, sourceManager);
    }
}
