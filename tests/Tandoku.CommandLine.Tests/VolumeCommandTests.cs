namespace Tandoku.CommandLine.Tests;

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
}
