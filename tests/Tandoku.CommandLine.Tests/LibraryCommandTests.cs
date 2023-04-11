namespace Tandoku.CommandLine.Tests;

using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;

public class LibraryCommandTests
{
    [Fact]
    public Task Init() => RunTest(
        "library init",
        @"Initialized new tandoku library at c:\temp\library.tdkl.yaml");

    [Fact]
    public Task InitWithPath() => RunTest(
        "library init tandoku-library",
        @$"Initialized new tandoku library at {Path.Combine(Directory.GetCurrentDirectory(), "tandoku-library", "library.tdkl.yaml")}");

    private static async Task RunTest(
        string commandLine,
        string expectedOutput,
        string? expectedError = null)
    {
        var (program, console, _) = SetUpProgram(Directory.GetCurrentDirectory());
        var result = await program.Run(commandLine);
        result.Should().Be(0);
        (console.Error.ToString()?.TrimEnd()).Should().Be(expectedError ?? string.Empty);
        (console.Out.ToString()?.TrimEnd()).Should().Be(expectedOutput);
    }

    private static (Program, TestConsole, MockFileSystem) SetUpProgram(
        string? startingDirectory = null)
    {
        var console = new TestConsole();
        var fileSystem = new MockFileSystem();

        if (startingDirectory is not null)
            fileSystem.Directory.SetCurrentDirectory(startingDirectory);

        return (
            new Program(console, fileSystem),
            console,
            fileSystem);
    }
}