namespace Tandoku.CommandLine.Tests;

using System.CommandLine.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

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

    protected async Task<ConsoleTestOutput> RunAsync(string commandLine)
    {
        var result = await this.program.RunAsync(commandLine);

        return new ConsoleTestOutput(
            result,
            this.console.Error.ToString(),
            this.console.Out.ToString());
    }

    protected async Task RunAndVerifyAsync(string commandLine, bool jsonOutput = false)
    {
        var output = await this.RunAsync($"{commandLine}{(jsonOutput ? " --json-output" : string.Empty)}");
        if (jsonOutput)
        {
            if (!string.IsNullOrWhiteSpace(output.Out))
            {
                // TODO: consider adding scrubbers to handle JSON encoded strings instead of converting JSON to YAML
                var deserializer = new Deserializer();
                var serializer = new Serializer();
                var o = deserializer.Deserialize(new StringReader(output.Out));
                output = output with { Out = serializer.Serialize(o) };
            }
            await VerifyYaml(output).UseParameters(jsonOutput);
        }
        else
        {
            await VerifyYaml(output);
        }
    }

    protected async Task RunAndVerifyVariantAsync(string commandLine, string snapshotMethodName, string variant)
    {
        var output = await this.RunAsync(commandLine);
        await VerifyYaml(output)
            .UseMethodName(snapshotMethodName)
            .IgnoreParametersForVerified(variant);
    }

    protected string ToFullPath(params string[] pathElements)
    {
        var relativePath = this.fileSystem.Path.Join(pathElements);
        return this.fileSystem.Path.Join(this.baseDirectory.FullName, relativePath);
    }

    protected record ConsoleTestOutput(
        int Result,
        [property: YamlMember(ScalarStyle = ScalarStyle.Literal)] string? Error,
        [property: YamlMember(ScalarStyle = ScalarStyle.Literal)] string? Out);
}
