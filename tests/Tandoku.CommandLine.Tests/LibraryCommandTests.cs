namespace Tandoku.CommandLine.Tests;

using System.CommandLine.IO;
using Tandoku.Library;
using Spectre.IO;
using Spectre.IO.Testing;

public class LibraryCommandTests
{
    private readonly TestConsole console;
    private readonly FakeEnvironment environment;
    private readonly FakeFileSystem fileSystem;
    private readonly DirectoryPath baseDirectory;
    private readonly Program program;

    public LibraryCommandTests()
    {
        this.console = new TestConsole();
        this.environment = new FakeEnvironment(PlatformFamily.Windows);
        this.fileSystem = new FakeFileSystem(this.environment);
        this.program = new Program(this.console, this.fileSystem, this.environment);

        // Note: currently using the current directory on the physical file system
        // as the base dir for the mock file system so that FileSystemInfo arguments
        // work correctly. May need to replace with an IFileSystemInfo-based implementation
        // later in order to allow for mock file system to properly support validation.
        this.baseDirectory = this.fileSystem.CreateDirectory(Directory.GetCurrentDirectory()).Path;
        this.environment.SetWorkingDirectory(this.baseDirectory);
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
    public Task InitWithFullPath() => this.RunAndAssertAsync(
        $"library init {this.ToFullPath("tandoku-library")}",
        $"Initialized new tandoku library at {this.ToFullPath("tandoku-library", "library.tdkl.yaml")}");

    [Fact]
    public async Task InitWithNonEmptyDirectory()
    {
        this.fileSystem.CreateFile(this.ToFullPath("tandoku-library", "existing.txt"));
        await this.RunAndAssertAsync(
            "library init tandoku-library",
            expectedOutput: string.Empty,
            expectedError: "The specified directory is not empty and force is not specified.");
    }

    [Fact]
    public async Task InitWithNonEmptyDirectoryForce()
    {
        this.fileSystem.CreateFile(this.ToFullPath("tandoku-library", "existing.txt"));
        await this.RunAndAssertAsync(
            "library init tandoku-library --force",
            $"Initialized new tandoku library at {this.ToFullPath("tandoku-library", "library.tdkl.yaml")}");
    }

    [Fact]
    public async Task Info()
    {
        var info = await this.SetupLibrary();
        this.environment.SetWorkingDirectory(info.Path);

        await this.RunAndAssertAsync(
            $"library info",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoInNestedPath()
    {
        var info = await this.SetupLibrary();
        var libraryDirectory = this.fileSystem.GetDirectory(info.Path);
        var nestedDirectory = this.fileSystem.CreateDirectory(libraryDirectory.Path.Combine("nested-directory"));
        this.environment.SetWorkingDirectory(nestedDirectory.Path);

        await this.RunAndAssertAsync(
            $"library info",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoInOtherPath()
    {
        await this.SetupLibrary();
        var otherDirectory = this.fileSystem.CreateDirectory(this.baseDirectory.Combine("other-directory"));
        this.environment.SetWorkingDirectory(otherDirectory.Path);

        await this.RunAndAssertAsync(
            $"library info",
            expectedError: "The specified path does not contain a tandoku library.");
    }

    [Fact]
    public async Task InfoInOtherPathWithEnvironment()
    {
        var info = await this.SetupLibrary();
        var otherDirectory = this.fileSystem.CreateDirectory(this.baseDirectory.Combine("other-directory"));
        this.environment.SetWorkingDirectory(otherDirectory.Path);
        this.environment.SetEnvironmentVariable("TANDOKU_LIBRARY", info.Path);

        await this.RunAndAssertAsync(
            $"library info",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoWithLibraryPath()
    {
        var info = await this.SetupLibrary();

        await this.RunAndAssertAsync(
            $"library info --library {info.Path}",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoWithDefinitionPath()
    {
        var info = await this.SetupLibrary();

        await this.RunAndAssertAsync(
            $"library info --library {info.DefinitionPath}",
            GetExpectedInfoOutput(info));
    }

    [Fact]
    public async Task InfoInvalidPath()
    {
        await this.SetupLibrary();

        await this.RunAndAssertAsync(
            $"library info --library does-not-exist",
            expectedError: "The specified path does not exist.");
    }

    private Task<LibraryInfo> SetupLibrary()
    {
        var libraryManager = new LibraryManager(this.fileSystem);
        var libraryRootPath = this.ToFullPath("tandoku-library");
        return libraryManager.InitializeAsync(libraryRootPath);
    }

    private static string GetExpectedInfoOutput(LibraryInfo info) =>
@$"Path: {info.Path}
Definition path: {info.DefinitionPath}
Language: {info.Definition.Language}
Reference language: {info.Definition.ReferenceLanguage}";

    private async Task RunAndAssertAsync(
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

    private string ToFullPath(params string[] pathElements)
    {
        var path = this.baseDirectory;
        for (int i = 0; i < pathElements.Length; i++)
        {
            path = path.Combine(pathElements[i]);
        }
        return path.FullPath;
    }
}