namespace Tandoku.CommandLine.Tests;

using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;
using Markdig.Helpers;

public class LibraryCommandTests
{
    private readonly string baseDir;
    private readonly TestConsole console;
    private readonly MockFileSystem fileSystem;
    private readonly Program program;

    public LibraryCommandTests()
    {
        // Note: currently using the current directory on the physical file system
        // as the base dir for the mock file system so that FileSystemInfo arguments
        // work correctly. May need to replace with an IFileSystemInfo-based implementation
        // later in order to allow for mock file system to properly support validation.
        this.baseDir = Directory.GetCurrentDirectory();

        this.console = new TestConsole();

        this.fileSystem = new MockFileSystem();
        this.fileSystem.Directory.SetCurrentDirectory(this.baseDir);

        this.program = new Program(this.console, this.fileSystem);
    }

    [Fact]
    public Task Init() => this.RunAndAssertAsync(
        "library init",
        $"Initialized new tandoku library at {this.ToFullPath("library.tdkl.yaml")}");

    [Fact]
    public Task InitWithPath() => this.RunAndAssertAsync(
        "library init tandoku-library",
        $"Initialized new tandoku library at {this.ToFullPath("tandoku-library", "library.tdkl.yaml")}");

    [Fact]
    public async Task InitWithNonEmptyDirectory()
    {
        this.fileSystem.AddEmptyFile(this.fileSystem.Path.Join(this.baseDir, "tandoku-library", "existing.txt"));
        await this.RunAndAssertAsync(
            "library init tandoku-library",
            expectedOutput: string.Empty,
            expectedError: "The specified directory is not empty and force is not specified.");
    }

    private async Task RunAndAssertAsync(
        string commandLine,
        string expectedOutput,
        string? expectedError = null,
        int? expectedResult = null)
    {
        var result = await this.program.RunAsync(commandLine);

        result.Should().Be(expectedResult ?? (string.IsNullOrEmpty(expectedError) ? 0 : 1));
        (this.console.Error.ToString()?.TrimEnd()).Should().Be(expectedError ?? string.Empty);
        (this.console.Out.ToString()?.TrimEnd()).Should().Be(expectedOutput);
    }

    private string ToFullPath(params string[] pathElements)
    {
        var relativePath = this.fileSystem.Path.Join(pathElements);
        return this.fileSystem.Path.GetFullPath(relativePath, this.baseDir);
    }
}