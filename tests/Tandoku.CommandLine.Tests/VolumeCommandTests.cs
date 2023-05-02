namespace Tandoku.CommandLine.Tests;

using Tandoku.Volume;

[UsesVerify]
public class VolumeCommandTests : CliTestBase
{
    [Fact]
    public async Task New()
    {
        await this.RunAndAssertAsync(
            "volume new sample-volume/1 --moniker sv-1 --tags tag1,tag2",
            @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("sv-1-sample-volume_1")}");

        this.fileSystem.GetFile(this.ToFullPath("sv-1-sample-volume_1", "volume.yaml")).TextContents.TrimEnd().Should().Be(
@"title: sample-volume/1
moniker: sv-1
language: ja
tags: [tag1, tag2]");
    }

    [Fact]
    public Task NewWithPath() => this.RunAndAssertAsync(
        "volume new sample-volume/1 --path container",
        @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("container", "sample-volume_1")}");

    [Fact]
    public Task NewWithFullPath() => this.RunAndAssertAsync(
        $"volume new sample-volume/1 --path {this.ToFullPath("container")}",
        @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("container", "sample-volume_1")}");

    [Fact]
    public async Task NewWithNonEmptyDirectory()
    {
        this.fileSystem.AddEmptyFile(this.ToFullPath("sample-volume", "existing.txt"));
        await this.RunAndAssertAsync(
            "volume new sample-volume",
            expectedError: "The specified directory is not empty and force is not specified.");
    }

    [Fact]
    public async Task NewWithNonEmptyDirectoryForce()
    {
        this.fileSystem.AddEmptyFile(this.ToFullPath("sample-volume", "existing.txt"));
        await this.RunAndAssertAsync(
            "volume new sample-volume --force",
            @$"Created new tandoku volume ""sample-volume"" at {this.ToFullPath("sample-volume")}");
    }


    [Fact]
    public async Task Info()
    {
        var info = await this.SetupVolume();
        this.fileSystem.Directory.SetCurrentDirectory(info.Path);

        await this.RunAndVerifyAsync("volume info");
    }

    [Theory]
    [InlineData(nameof(InfoInNestedPath))]
    public async Task InfoInNestedPath(string variant)
    {
        var info = await this.SetupVolume();
        var volumeDirectory = this.fileSystem.GetDirectory(info.Path);
        var nestedDirectory = volumeDirectory.CreateSubdirectory("nested-directory");
        this.fileSystem.Directory.SetCurrentDirectory(nestedDirectory.FullName);

        await this.RunAndVerifyVariantAsync(
            "volume info",
            nameof(Info),
            variant);
    }

    [Fact]
    public async Task InfoInOtherPath()
    {
        await this.SetupVolume();
        var otherDirectory = this.baseDirectory.CreateSubdirectory("other-directory");
        this.fileSystem.Directory.SetCurrentDirectory(otherDirectory.FullName);

        await this.RunAndAssertAsync(
            $"volume info",
            expectedError: "The specified path does not contain a tandoku volume.");
    }

    [Theory]
    [InlineData(nameof(InfoWithVolumePath))]
    public async Task InfoWithVolumePath(string variant)
    {
        var info = await this.SetupVolume();

        await this.RunAndVerifyVariantAsync(
            @$"volume info --volume ""{info.Path}""",
            nameof(Info),
            variant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".")]
    public async Task List(string pathArgument)
    {
        var rootPath = this.fileSystem.Directory.GetCurrentDirectory();
        await this.SetupVolume("volume1", rootPath);
        await this.SetupVolume("volume2", rootPath);
        await this.SetupVolume("nested-volume", this.fileSystem.Path.Join(rootPath, "nested"));

        var output = await this.RunAsync(
            pathArgument is null ? "volume list" : $"volume list {pathArgument}");
        await VerifyYaml(output)
            .IgnoreParametersForVerified(pathArgument);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_Nested(bool changeCurrentDirectory)
    {
        var rootPath = this.fileSystem.Directory.GetCurrentDirectory();
        var nestedPath = this.fileSystem.Path.Join(rootPath, "nested");
        await this.SetupVolume("volume1", rootPath);
        await this.SetupVolume("nested-volume", nestedPath);
        await this.SetupVolume("nested-volume2", nestedPath);

        if (changeCurrentDirectory)
            this.fileSystem.Directory.SetCurrentDirectory(nestedPath);

        var output = await this.RunAsync(
            changeCurrentDirectory ? "volume list": $"volume list {nestedPath}");
        await VerifyYaml(output)
            .IgnoreParametersForVerified(changeCurrentDirectory);
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
