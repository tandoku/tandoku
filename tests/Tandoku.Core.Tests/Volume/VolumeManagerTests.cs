namespace Tandoku.Tests.Volume;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Volume;

public class VolumeManagerTests
{
    private readonly MockFileSystem fileSystem;
    private readonly VolumeManager volumeManager;

    public VolumeManagerTests()
    {
        this.fileSystem = new MockFileSystem();
        this.volumeManager = new VolumeManager(this.fileSystem);
    }

    [Fact]
    public async Task Initialize()
    {
        var volumeRootPath = this.GetVolumeRootPath();
        //var definitionPath = this.fileSystem.Path.Join(volumeRootPath, "volume.yaml");

        var info = await this.volumeManager.InitializeAsync(volumeRootPath);

        info.Path.Should().Be(volumeRootPath);
        info.Version.Should().Be(VolumeVersion.Latest);
        //info.DefinitionPath.Should().Be(definitionPath);
        //fileSystem.AllFiles.Count().Should().Be(2);
        this.fileSystem.GetFile(this.fileSystem.Path.Join(info.Path, ".tandoku-volume/version")).TextContents.Should().Be(
            VolumeVersion.Latest.Version.ToString());
//        fileSystem.GetFile(definitionPath).TextContents.TrimEnd().Should().Be(
//@"language: ja
//referenceLanguage: en");
    }

    //[Fact]
    //public async Task GetInfo()
    //{
    //    var volumeRootPath = this.GetVolumeRootPath();
    //    var originalInfo = await this.volumeManager.InitializeAsync(volumeRootPath);

    //    var info = await this.volumeManager.GetInfoAsync(originalInfo.Path);

    //    info.Should().BeEquivalentTo(originalInfo);
    //}

    private string GetVolumeRootPath()
    {
        return this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "tandoku-volume");
    }
}
