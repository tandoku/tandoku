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
    public async Task CreateNew()
    {
        var title = "sample volume/1";
        var containerPath = this.fileSystem.Directory.GetCurrentDirectory();

        var info = await this.volumeManager.CreateNewAsync(title, containerPath);

        info.Path.Should().Be(this.fileSystem.Path.Join(containerPath, "sample volume_1"));
        info.Version.Should().Be(VolumeVersion.Latest);

        var definitionPath = this.fileSystem.Path.Join(info.Path, "volume.yaml");
        info.DefinitionPath.Should().Be(definitionPath);
        this.fileSystem.AllFiles.Count().Should().Be(2);
        this.fileSystem.GetFile(this.fileSystem.Path.Join(info.Path, ".tandoku-volume/version")).TextContents.Should().Be(
            VolumeVersion.Latest.Version.ToString());
        this.fileSystem.GetFile(definitionPath).TextContents.TrimEnd().Should().Be(
@"title: sample volume/1
language: ja
referenceLanguage: en");
    }

    // TODO: add tests for moniker

    [Fact]
    public async Task GetInfo()
    {
        var originalInfo = await this.SetupVolume();

        var info = await this.volumeManager.GetInfoAsync(originalInfo.Path);

        info.Should().BeEquivalentTo(originalInfo);
    }

    private Task<VolumeInfo> SetupVolume(string title = "sample volume")
    {
        var containerPath = this.fileSystem.Directory.GetCurrentDirectory();
        return this.volumeManager.CreateNewAsync(title, containerPath);
    }
}
