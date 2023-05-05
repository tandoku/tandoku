namespace Tandoku.Tests.Volume;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Library;
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
language: ja");
//referenceLanguage: en");
    }

    [Fact]
    public async Task CreateNew2()
    {
        var title = "sample volume/2";
        var containerPath = this.fileSystem.Directory.GetCurrentDirectory();

        var info = await this.volumeManager.CreateNewAsync(
            title,
            containerPath,
            moniker: "sv-2",
            tags: new[] { "tag-1", "tag-2" });

        info.Path.Should().Be(this.fileSystem.Path.Join(containerPath, "sv-2-sample volume_2"));
        this.fileSystem.AllFiles.Count().Should().Be(2);
        this.fileSystem.GetFile(info.DefinitionPath).TextContents.TrimEnd().Should().Be(
@"title: sample volume/2
moniker: sv-2
language: ja
tags: [tag-1, tag-2]");
    }

    [Fact]
    public async Task GetInfo()
    {
        var originalInfo = await this.SetupVolume();

        var info = await this.volumeManager.GetInfoAsync(originalInfo.Path);

        info.Should().BeEquivalentTo(originalInfo);
    }

    [Fact]
    public async Task GetInfo2()
    {
        var originalInfo = await this.SetupVolume(moniker: "v1", tags: new[] { "tag1", "tag2" });

        var info = await this.volumeManager.GetInfoAsync(originalInfo.Path);

        info.Should().BeEquivalentTo(originalInfo);
    }

    [Fact]
    public async Task GetVolumeDirectories()
    {
        var rootPath = this.fileSystem.Directory.GetCurrentDirectory();
        await this.SetupVolume("volume1", rootPath);
        await this.SetupVolume("volume2", rootPath);
        await this.SetupVolume("nested-volume", this.fileSystem.Path.Join(rootPath, "nested"));

        var result = this.volumeManager.GetVolumeDirectories(rootPath);

        result.Should().BeEquivalentTo(new[]
        {
            this.fileSystem.Path.Join(rootPath, "volume1"),
            this.fileSystem.Path.Join(rootPath, "volume2"),
            this.fileSystem.Path.Join(rootPath, "nested", "nested-volume"),
        });
    }

    [Fact]
    public void GetVolumeDirectories_NotExists()
    {
        var rootPath = this.fileSystem.Directory.GetCurrentDirectory();

        var result = this.volumeManager.GetVolumeDirectories(
            this.fileSystem.Path.Join(rootPath, "not-exists"));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVolumeDirectories_WithinVolumeDirectory()
    {
        var info = await this.SetupVolume("sample-volume");
        var nestedPath = this.fileSystem.GetDirectory(info.Path)
            .CreateSubdirectory("nested").FullName;

        var result = this.volumeManager.GetVolumeDirectories(nestedPath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVolumeDirectories_WithinVolumeDirectory_ExpandScopeVolume()
    {
        var info = await this.SetupVolume("sample-volume");
        var nestedPath = this.fileSystem.GetDirectory(info.Path)
            .CreateSubdirectory("nested").FullName;

        var result = this.volumeManager.GetVolumeDirectories(nestedPath, ExpandedScope.ParentVolume);

        result.Should().BeEquivalentTo(new[]
        {
            info.Path
        });
    }

    [Fact]
    public async Task GetVolumeDirectories_ExpandScopeLibrary()
    {
        var libraryPath = (await this.SetupLibrary()).Path;
        var nestedPath = this.fileSystem.Path.Join(libraryPath, "nested");
        await this.SetupVolume("volume1", libraryPath);
        await this.SetupVolume("nested-volume", nestedPath);
        var info = await this.SetupVolume("nested-volume2", nestedPath);

        var result = this.volumeManager.GetVolumeDirectories(info.Path, ExpandedScope.ParentLibrary);

        result.Should().BeEquivalentTo(new[]
        {
            this.fileSystem.Path.Join(libraryPath, "volume1"),
            this.fileSystem.Path.Join(libraryPath, "nested", "nested-volume"),
            this.fileSystem.Path.Join(libraryPath, "nested", "nested-volume2"),
        });
    }

    [Fact]
    public async Task GetVolumeDirectories_ExpandScopeLibrary_NoLibrary()
    {
        var rootPath = this.fileSystem.Directory.GetCurrentDirectory();
        var info = await this.SetupVolume("volume1", rootPath);

        this.volumeManager.Invoking(m => m.GetVolumeDirectories(info.Path, ExpandedScope.ParentLibrary))
            .Should().Throw<ArgumentException>();
    }

    private Task<VolumeInfo> SetupVolume(
        string title = "sample volume",
        string? containerPath = null,
        string? moniker = null,
        IEnumerable<string>? tags = null)
    {
        containerPath ??= this.fileSystem.Directory.GetCurrentDirectory();
        return this.volumeManager.CreateNewAsync(title, containerPath, moniker, tags);
    }

    private Task<LibraryInfo> SetupLibrary()
    {
        var path = this.fileSystem.Path.Join(
            this.fileSystem.Directory.GetCurrentDirectory(),
            "tandoku-library");

        var libraryManager = new LibraryManager(this.fileSystem);
        return libraryManager.InitializeAsync(path);
    }
}
