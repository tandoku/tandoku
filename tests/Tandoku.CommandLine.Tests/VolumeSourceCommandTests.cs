namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Volume;

[UsesVerify]
public class VolumeSourceCommandTests : CliTestBase
{
    [Fact]
    public async Task Import()
    {
        var externalFilePath = this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "external",
            "volume-source.txt");
        this.fileSystem.AddFile(externalFilePath, new MockFileData("source file content"));

        var volumeInfo = await this.SetupVolume();
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalFilePath}""");
        this.fileSystem.GetFile(this.ToFullPath("sample volume", "source", "volume-source.txt")).TextContents.TrimEnd()
            .Should().Be("source file content");
    }

    private Task<VolumeInfo> SetupVolume(
        string title = "sample volume",
        string? containerPath = null,
        string? moniker = null,
        IEnumerable<string>? tags = null)
    {
        containerPath ??= this.fileSystem.Directory.GetCurrentDirectory();

        var volumeManager = new VolumeManager(this.fileSystem);
        return volumeManager.CreateNewAsync(title, containerPath, moniker, tags);
    }
}
