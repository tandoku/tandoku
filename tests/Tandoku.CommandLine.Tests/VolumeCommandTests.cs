namespace Tandoku.CommandLine.Tests;

public class VolumeCommandTests : CliTestBase
{
    [Fact]
    public Task New() => this.RunAndAssertAsync(
        "volume new sample-volume/1",
        @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("sample-volume_1")}");

    [Fact]
    public Task NewWithPath() => this.RunAndAssertAsync(
        "volume new sample-volume/1 --path container",
        @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("container", "sample-volume_1")}");

    [Fact]
    public Task NewWithFullPath() => this.RunAndAssertAsync(
        $"volume new sample-volume/1 --path {this.ToFullPath("container")}",
        @$"Created new tandoku volume ""sample-volume/1"" at {this.ToFullPath("container", "sample-volume_1")}");
}
