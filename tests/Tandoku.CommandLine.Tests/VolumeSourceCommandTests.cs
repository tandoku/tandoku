namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Volume;

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
        var volumeDir = this.fileSystem.GetDirectory(volumeInfo.Path);
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalFilePath}""", jsonOutput);
        this.fileSystem.GetFile(volumeDir.GetPath("source/volume-source.txt")).TextContents.TrimEnd()
            .Should().Be("source file content");
    }

    [Fact]
    public async Task ImportWithWildcard()
    {
        var externalDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("external");
        var file1 = externalDir.GetFile("source1.txt");
        var file2 = externalDir.GetFile("source2.txt");
        this.fileSystem.AddFile(file1, new MockFileData("content1"));
        this.fileSystem.AddFile(file2, new MockFileData("content2"));

        var volumeInfo = await this.SetupVolume();
        var volumeDir = this.fileSystem.GetDirectory(volumeInfo.Path);
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalDir}/*.txt""");
        this.fileSystem.GetFile(volumeDir.GetPath("source/source1.txt")).TextContents.TrimEnd()
            .Should().Be("content1");
        this.fileSystem.GetFile(volumeDir.GetPath("source/source2.txt")).TextContents.TrimEnd()
            .Should().Be("content2");
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
        var volumeDir = this.fileSystem.GetDirectory(volumeInfo.Path);
        this.fileSystem.Directory.SetCurrentDirectory(volumeInfo.Path);

        await this.RunAndVerifyAsync(@$"source import ""{externalFilePath}"" --filename src.txt", jsonOutput);
        this.fileSystem.GetFile(volumeDir.GetPath("source/src.txt")).TextContents.TrimEnd()
            .Should().Be("source file content");
    }

    private Task<VolumeInfo> SetupVolume()
    {
        var path = this.fileSystem.GetCurrentDirectory().GetPath("sample-volume");

        var volumeManager = new VolumeManager(this.fileSystem);
        return volumeManager.InitializeAsync(path);
    }
}
