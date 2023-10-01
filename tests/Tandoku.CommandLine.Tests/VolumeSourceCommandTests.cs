namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Volume;

[UsesVerify]
public class VolumeSourceCommandTests : CliTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import(bool jsonOutput)
    {
        var externalFilePath = this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "external",
            "volume-source.txt");
        this.fileSystem.AddFile(externalFilePath, new MockFileData("source file content"));

        var volumeInfo = await this.SetupVolume();
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalFilePath}""", jsonOutput);
        this.fileSystem.GetFile(this.ToFullPath("sample volume", "source", "volume-source.txt")).TextContents.TrimEnd()
            .Should().Be("source file content");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportWithFileName(bool jsonOutput)
    {
        var externalFilePath = this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "external",
            "volume-source.txt");
        this.fileSystem.AddFile(externalFilePath, new MockFileData("source file content"));

        var volumeInfo = await this.SetupVolume();
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalFilePath}"" --filename src.txt", jsonOutput);
        this.fileSystem.GetFile(this.ToFullPath("sample volume", "source", "src.txt")).TextContents.TrimEnd()
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
