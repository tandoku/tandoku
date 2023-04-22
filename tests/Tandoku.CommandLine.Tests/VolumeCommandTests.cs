﻿namespace Tandoku.CommandLine.Tests;

using Tandoku.Volume;

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
            expectedOutput: string.Empty,
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

        await this.RunAndAssertAsync(
            $"volume info",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoInNestedPath()
    {
        var info = await this.SetupVolume();
        var volumeDirectory = this.fileSystem.GetDirectory(info.Path);
        var nestedDirectory = volumeDirectory.CreateSubdirectory("nested-directory");
        this.fileSystem.Directory.SetCurrentDirectory(nestedDirectory.FullName);

        await this.RunAndAssertAsync(
            $"volume info",
            GetExpectedInfoOutput(info));
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

    [Fact]
    public async Task InfoWithVolumePath()
    {
        var info = await this.SetupVolume();

        await this.RunAndAssertAsync(
            @$"volume info --volume ""{info.Path}""",
            GetExpectedInfoOutput(info));
    }

    private Task<VolumeInfo> SetupVolume()
    {
        var volumeManager = new VolumeManager(this.fileSystem);
        return volumeManager.CreateNewAsync(
            "sample volume",
            this.fileSystem.Directory.GetCurrentDirectory());
    }

    // TODO: switch to Verify for these tests?
    private static string GetExpectedInfoOutput(VolumeInfo info) =>
@$"Path: {info.Path}
Version: {info.Version.Version}
Definition path: {info.DefinitionPath}
Title: {info.Definition.Title}
Moniker: {info.Definition.Moniker ?? "<none>"}
Language: {info.Definition.Language}
Tags: {(info.Definition.Tags.Any() ? string.Join(", ", info.Definition.Tags) : "<none>")}";
//Reference language: {info.Definition.ReferenceLanguage}";
}