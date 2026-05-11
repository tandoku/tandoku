namespace Tandoku.CommandLine;

using System.CommandLine;
using System.IO.Abstractions;
using System.Text;
using Tandoku.CommandLine.Abstractions;

public sealed partial class Program
{
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly IFileSystem fileSystem;
    private readonly IEnvironment environment;

    private readonly Option<bool> jsonOutputOption = new("--json-output")
    {
        Description = "Output results as JSON",
        Recursive = true,
    };
#if DEBUG
    private readonly Directive debugDirective = new("debug");
#endif

    public Program(
        TextWriter? output = null,
        TextWriter? error = null,
        IFileSystem? fileSystem = null,
        IEnvironment? environment = null)
    {
        this.output = output ?? Console.Out;
        this.error = error ?? Console.Error;
        this.fileSystem = fileSystem ?? new FileSystem();
        this.environment = environment ?? new SystemEnvironment();
    }

    [STAThread] // TODO: needed? should use MTAThread instead?
    public static Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        return new Program().RunAsync(args);
    }

    public Task<int> RunAsync(string[] args)
    {
        var rootCommand = this.CreateRootCommand();
        var parseResult = rootCommand.Parse(args);
        return this.InvokeAsync(parseResult);
    }

    public Task<int> RunAsync(string commandLine)
    {
        var rootCommand = this.CreateRootCommand();
        var parseResult = rootCommand.Parse(commandLine);
        return this.InvokeAsync(parseResult);
    }

    private async Task<int> InvokeAsync(ParseResult parseResult)
    {
#if DEBUG
        if (parseResult.GetResult(this.debugDirective) is not null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        var config = new InvocationConfiguration
        {
            Output = this.output,
            Error = this.error,
            EnableDefaultExceptionHandler = false,
        };

        // TODO: consider catching only known app-specific exceptions instead
        try
        {
            return await parseResult.InvokeAsync(config);
        }
        catch (ArgumentException exception)
        {
            return HandleKnownException(exception);
        }
        catch (InvalidOperationException exception)
        {
            return HandleKnownException(exception);
        }

        int HandleKnownException(Exception exception)
        {
            // TODO - switch to Spectre.Console rendering
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            this.error.WriteLine(exception.Message);
            Console.ForegroundColor = originalColor;
            return 1;
        }
    }

    private RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("Command-line interface for tandoku")
        {
            this.CreateLibraryCommand(),
            this.CreateVolumeCommand(),
            this.CreateSourceCommand(),
            this.CreateContentCommand(),
            this.CreateSubtitlesCommand(),
        };
        rootCommand.Options.Add(this.jsonOutputOption);
#if DEBUG
        rootCommand.Directives.Add(this.debugDirective);
#endif
        return rootCommand;
    }
}
