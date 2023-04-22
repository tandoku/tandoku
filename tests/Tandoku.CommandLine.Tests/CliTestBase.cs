namespace Tandoku.CommandLine.Tests;

using System.CommandLine.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.CommandLine.Tests.Abstractions;

public abstract class CliTestBase
{
    protected readonly TestConsole console;
    protected readonly MockFileSystem fileSystem;
    protected readonly IDirectoryInfo baseDirectory;
    protected readonly MockEnvironment environment;
    protected readonly Program program;

    protected CliTestBase()
    {
        this.console = new TestConsole();
        this.fileSystem = new MockFileSystem();
        this.environment = new MockEnvironment();
        this.program = new Program(this.console, this.fileSystem, this.environment);

        // Note: currently using the current directory on the physical file system
        // as the base dir for the mock file system so that FileSystemInfo arguments
        // work correctly. May need to replace with an IFileSystemInfo-based implementation
        // later in order to allow for mock file system to properly support validation.
        this.baseDirectory = this.fileSystem.GetDirectory(Directory.GetCurrentDirectory());
        this.fileSystem.Directory.SetCurrentDirectory(this.baseDirectory.FullName);
    }

    protected async Task RunAndAssertAsync(
        string commandLine,
        string? expectedOutput = null,
        string? expectedError = null,
        int? expectedResult = null)
    {
        var result = await this.program.RunAsync(commandLine);

        // Note: check Error first (and result code last) as this is most useful if test unexpectedly fails
        (this.console.Error.ToString()?.TrimEnd()).Should().Be(expectedError ?? string.Empty);
        (this.console.Out.ToString()?.TrimEnd()).Should().Be(expectedOutput ?? string.Empty);
        result.Should().Be(expectedResult ?? (string.IsNullOrEmpty(expectedError) ? 0 : 1));
    }

    protected string ToFullPath(params string[] pathElements)
    {
        var relativePath = this.fileSystem.Path.Join(pathElements);
        return this.fileSystem.Path.Join(this.baseDirectory.FullName, relativePath);
    }
}
