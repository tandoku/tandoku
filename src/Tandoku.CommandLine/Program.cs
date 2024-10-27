namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using System.Text;
using Spectre.Console;
using Tandoku.CommandLine.Abstractions;

public sealed partial class Program
{
    private readonly IAnsiConsole console;
    private readonly IConsole consoleWrapper;
    private readonly IFileSystem fileSystem;
    private readonly IEnvironment environment;

    private readonly Option<bool> jsonOutputOption = new(["--json-output"], "Output results as JSON");

    public Program(
        IAnsiConsole? console = null,
        IFileSystem? fileSystem = null,
        IEnvironment? environment = null)
    {
        if (console is not null)
        {
            this.console = console;
            this.consoleWrapper = new AnsiConsoleWrapper(console);
        }
        else
        {
            this.console = AnsiConsole.Console;
            this.consoleWrapper = new SystemConsole();
        }
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
        var parser = this.BuildCommandLineParser();
        return parser.InvokeAsync(args, this.consoleWrapper);
    }
    public Task<int> RunAsync(string commandLine)
    {
        var parser = this.BuildCommandLineParser();
        return parser.InvokeAsync(commandLine, this.consoleWrapper);
    }

    private Parser BuildCommandLineParser()
    {
        var parser = new CommandLineBuilder(this.CreateRootCommand())
#if DEBUG
            .AddMiddleware(context =>
            {
                if (context.ParseResult.Directives.Contains("debug"))
                {
                    System.Diagnostics.Debugger.Launch();
                }
            }, MiddlewareOrder.Default)
#endif
//#if !DEBUG // TODO: consider catching only known app-specific exceptions instead (is ArgumentException thrown by System.CommandLine parsing?)
            .AddMiddleware(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (ArgumentException exception)
                {
                    HandleKnownException(exception, context);
                }
                catch (InvalidOperationException exception)
                {
                    HandleKnownException(exception, context);
                }
            }, MiddlewareOrder.ExceptionHandler)
//#endif
            .UseDefaults()
            .Build();

//#if !DEBUG
        static void HandleKnownException(Exception exception, InvocationContext context)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{exception.Message}[/]");
            context.ExitCode = 1;
        }
//#endif

        return parser;
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

            // Legacy commands
            CreateGenerateCommand(),
            CreateExportCommand(),
            CreateTokenizeCommand(),
            CreateTransformCommand(),
            CreateComputeCommand(),

            Demos.CreateCommand(),
        };
        rootCommand.AddGlobalOption(this.jsonOutputOption);
        return rootCommand;
    }

    // Legacy commands

    private static Command CreateGenerateCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly();
        var inputTypeOpt = new Option<ContentGeneratorInputType?>(new[] { "-t", "--input-type" }, "Type of input (derived from extension if not specified)");
        var outputOpt = new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path").LegalFilePathsOnly();
        var appendOpt = new Option<bool>(new[] { "-a", "--append" }, "Append to existing content");
        var forceOpt = new Option<bool>(new[] { "-f", "--force", "--overwrite" }, "Overwrite existing content");
        var command = new Command("generate", "Generate tandoku content from various input formats")
        {
            inputArg,
            inputTypeOpt,
            outputOpt,
            appendOpt,
            forceOpt,
        };
        command.SetHandler(
            (FileSystemInfo[] @in, ContentGeneratorInputType? inputType, FileInfo? @out, bool append, bool force) =>
            {
                var generator = new ContentGenerator();
                var outputBehavior = append ? ContentOutputBehavior.Append :
                    force ? ContentOutputBehavior.Overwrite :
                    ContentOutputBehavior.None;
                var outPath = generator.Generate(@in.Select(i => i.FullName), inputType, @out?.FullName, outputBehavior);
                Console.WriteLine($"Generated {outPath}");
            }, inputArg, inputTypeOpt, outputOpt, appendOpt, forceOpt);
        return command;
    }

    private static Command CreateExportCommand()
    {
        var inputArg = new Argument<FileSystemInfo>("in", "Input file or path").ExistingOnly();
        var outputArg = new Argument<FileInfo>("out", "Output file path") { Arity = ArgumentArity.ZeroOrOne }.LegalFilePathsOnly();
        var formatOpt = new Option<ExportFormat>("--format", "Target file format");
        var command = new Command("export", "Export content from tandoku library")
        {
            inputArg,
            outputArg,
            formatOpt,
        };
        command.SetHandler(
            (FileSystemInfo @in, FileInfo? @out, ExportFormat format) =>
            {
                var exporter = new Exporter();
                var outPath = exporter.Export(@in.FullName, @out?.FullName, format);
                Console.WriteLine($"Exported {outPath}");
            }, inputArg, outputArg, formatOpt);
        return command;
    }

    private static Command CreateTokenizeCommand()
    {
        var inputArg = new Argument<FileInfo>("in", "Input file or path").ExistingOnly();
        var command = new Command("tokenize", "Tokenize text content")
        {
            inputArg,
        };
        command.SetHandler(
            (FileInfo @in) =>
            {
                var processor = new TextProcessor();
                processor.Tokenize(@in.FullName);
                Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
            }, inputArg);
        return command;
    }

    private static Command CreateTransformCommand()
    {
        var transformListArg = new Argument<string>("transform-list", "Comma-separated list of transforms to apply");
        var inputArg = new Argument<FileInfo>("in", "Input file or path").ExistingOnly();
        var command = new Command("transform", "Apply transforms to text content")
        {
            transformListArg,
            inputArg,
        };
        command.SetHandler(
            (string transformList, FileInfo @in) =>
            {
                var transforms = transformList.Split(',')
                    .Select(s => Enum.Parse<ContentTransformKind>(s, ignoreCase: true));

                var processor = new TextProcessor();
                processor.Transform(@in.FullName, transforms);
                Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
            }, transformListArg, inputArg);
        return command;
    }

    private static Command CreateComputeCommand() =>
        new Command("compute", "Compute statistics")
        {
            CreateComputeContentCommand(),
            CreateComputeAggregatesCommand(),
            CreateComputeAnalyticsCommand(),
        };

    private static Command CreateComputeContentCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly();
        var outputOpt = new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path") { IsRequired = true }.LegalFilePathsOnly();
        var command = new Command("content", "Compute statistics for content")
        {
            inputArg,
            outputOpt,
        };
        command.SetHandler(
            (FileSystemInfo[] @in, FileInfo @out) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeStats(@in, @out.FullName);
                Console.WriteLine($"Computed statistics written to {@out.FullName}");
            }, inputArg, outputOpt);
        return command;
    }

    private static Command CreateComputeAggregatesCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly();
        var outputOpt = new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path") { IsRequired = true }.LegalFilePathsOnly();
        var command = new Command("aggregates", "Compute aggregate statistics")
        {
            inputArg,
            outputOpt,
        };
        command.SetHandler(
            (FileSystemInfo[] @in, FileInfo @out) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeAggregates(@in, @out.FullName);
                Console.WriteLine($"Computed aggregates written to {@out.FullName}");
            }, inputArg, outputOpt);
        return command;
    }

    private static Command CreateComputeAnalyticsCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly();
        var corpusOpt = new Option<FileInfo>(new[] { "-c", "--corpus" }, "Corpus aggregates path") { IsRequired = true }.LegalFilePathsOnly();
        var command = new Command("analytics", "Compute analytics for a volume in context of corpus aggregates")
        {
            inputArg,
            corpusOpt,
        };
        command.SetHandler(
            (FileSystemInfo[] @in, FileInfo corpus) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeAnalytics(@in, corpus.FullName);
                Console.WriteLine($"Computed analytics written to input files");
            }, inputArg, corpusOpt);
        return command;
    }

    private class AnsiConsoleWrapper(IAnsiConsole console) : IConsole
    {
        public bool IsInputRedirected => false;
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public IStandardStreamWriter Out { get; } = new StandardStreamWriter(console);
        public IStandardStreamWriter Error { get; } = new StandardStreamWriter(console); // TODO - IAnsiConsole doesn't have error output...

        private sealed class StandardStreamWriter(IAnsiConsole console) : IStandardStreamWriter
        {
            public void Write(string? value)
            {
                if (value is not null)
                    console.Write(value);
            }
        }
    }
}
