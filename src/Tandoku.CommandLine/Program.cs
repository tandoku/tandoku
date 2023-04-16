namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.NamingConventionBinder; // TODO: remove when migrated to new SetHandler methods
using System.CommandLine.Parsing;
using System.CommandLine.Rendering;
using System.IO.Abstractions;
using Tandoku.Library;

public sealed class Program
{
    private readonly IConsole console;
    private readonly IFileSystem fileSystem;
    private readonly IEnvironment environment;

    public Program(
        IConsole? console = null,
        IFileSystem? fileSystem = null,
        IEnvironment? environment = null)
    {
        this.console = console ?? new SystemConsole();
        this.fileSystem = fileSystem ?? new FileSystem();
        this.environment = environment ?? new EnvironmentWrapper();
    }

    [STAThread] // TODO: needed? should use MTAThread instead?
    public static Task<int> Main(string[] args)
    {
        // TODO: switch codepage to UTF-8?

        return new Program().RunAsync(args);
    }

    public Task<int> RunAsync(string[] args)
    {
        var parser = this.BuildCommandLineParser();
        return parser.InvokeAsync(args, this.console);
    }
    public Task<int> RunAsync(string commandLine)
    {
        var parser = this.BuildCommandLineParser();
        return parser.InvokeAsync(commandLine, this.console);
    }

    private Parser BuildCommandLineParser()
    {
        // TODO: the [debug] directive is missing in latest CommandLine library

        return new CommandLineBuilder(this.CreateRootCommand())
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
            }, MiddlewareOrder.ExceptionHandler)
            .UseDefaults()
            .Build();

        static void HandleKnownException(Exception exception, InvocationContext context)
        {
            var terminal = context.Console.GetTerminal(preferVirtualTerminal: false);
            terminal.ResetColor();
            terminal.ForegroundColor = ConsoleColor.Red;

            terminal.Error.WriteLine(exception.Message);

            terminal.ResetColor();

            context.ExitCode = 1;
        }
    }

    private RootCommand CreateRootCommand() =>
        new("Command-line interface for tandoku")
        {
            this.CreateLibraryCommand(),

            // Legacy commands
            CreateGenerateCommand(),
            CreateExportCommand(),
            CreateTokenizeCommand(),
            CreateTransformCommand(),
            CreateComputeCommand(),

            Demos.CreateCommand(),
        };

    private Command CreateLibraryCommand() =>
        new("library", "Commands for working with tandoku libraries")
        {
            this.CreateLibraryInitCommand(),
            this.CreateLibraryInfoCommand(),
        };

    private Command CreateLibraryInitCommand()
    {
        var pathArgument = new Argument<DirectoryInfo?>("path", "Directory for new tandoku library")
        {
            Arity = ArgumentArity.ZeroOrOne,
        }.LegalFilePathsOnly();
        var forceOption = new Option<bool>(new[] { "--force", "-f" }, "Allow new library in non-empty directory");

        var command = new Command("init", "Initializes a new tandoku library in the current or specified directory")
        {
            pathArgument,
            forceOption,
        };

        command.SetHandler(async (DirectoryInfo? pathInfo, bool force) =>
        {
            var libraryManager = this.CreateLibraryManager();
            var info = await libraryManager.InitializeAsync(pathInfo?.FullName, force);
            this.console.WriteLine($"Initialized new tandoku library at {info.DefinitionPath}");
        }, pathArgument, forceOption);

        return command;
    }

    private Command CreateLibraryInfoCommand()
{
        var libraryBinder = new LibraryBinder(this.fileSystem, this.environment, this.CreateLibraryManager);

        var command = new Command("info", "Displays information about the current or specified library")
        {
            libraryBinder.LibraryOption,
        };

        command.SetHandler(async (IFileInfo libraryDefinitionFile) =>
        {
            var libraryManager = this.CreateLibraryManager();
            var info = await libraryManager.GetInfoAsync(libraryDefinitionFile.FullName);
            this.console.WriteLine($"Path: {info.Path}");
            this.console.WriteLine($"Definition path: {info.DefinitionPath}");
            this.console.WriteLine($"Language: {info.Definition.Language}");
            this.console.WriteLine($"Reference language: {info.Definition.ReferenceLanguage}");
        }, libraryBinder);

        return command;
    }

    private LibraryManager CreateLibraryManager() => new(this.fileSystem);

    private sealed class LibraryBinder : BinderBase<IFileInfo>
    {
        private readonly IFileSystem fileSystem;
        private readonly IEnvironment environment;
        private readonly Func<LibraryManager> createLibraryManager;

        internal LibraryBinder(IFileSystem fileSystem, IEnvironment environment, Func<LibraryManager> createLibraryManager)
        {
            this.fileSystem = fileSystem;
            this.environment = environment;
            this.createLibraryManager = createLibraryManager;

            this.LibraryOption = new Option<FileSystemInfo?>(
                new[] { "-l", "--library" },
                "Library directory or definition (.tdkl.yaml) path")
                .LegalFilePathsOnly();
        }

        internal Option<FileSystemInfo?> LibraryOption { get; }

        protected override IFileInfo GetBoundValue(BindingContext bindingContext)
        {
            var fileSysInfo = bindingContext.ParseResult.GetValueForOption(this.LibraryOption);

            var libraryManager = this.createLibraryManager();

            var libraryDefinitionPath = fileSysInfo is not null ?
                libraryManager.ResolveLibraryDefinitionPath(fileSysInfo.FullName) :
                libraryManager.ResolveLibraryDefinitionPath(this.fileSystem.Directory.GetCurrentDirectory(), checkAncestors: true);

            if (libraryDefinitionPath is null &&
                this.environment.GetEnvironmentVariable("TANDOKU_LIBRARY") is string envPath)
            {
                libraryDefinitionPath = libraryManager.ResolveLibraryDefinitionPath(envPath);
            }

            return libraryDefinitionPath is not null ?
                this.fileSystem.FileInfo.New(libraryDefinitionPath) :
                throw new ArgumentException("The specified path does not contain a tandoku library.");
        }
    }

    // Legacy commands

    private static Command CreateGenerateCommand() =>
        new Command("generate", "Generate tandoku content from various input formats")
        {
            new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly(),
            new Option<ContentGeneratorInputType?>(new[] { "-t", "--input-type" }, "Type of input (derived from extension if not specified)"),
            new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path").LegalFilePathsOnly(),
            new Option<bool>(new[]{"-a", "--append"}, "Append to existing content"),
            new Option<bool>(new[]{"-f", "--force", "--overwrite"}, "Overwrite existing content"),
        }.WithHandler(CommandHandler.Create(
            (FileSystemInfo[] @in, ContentGeneratorInputType? inputType, FileInfo? @out, bool append, bool force) =>
            {
                var generator = new ContentGenerator();
                var outputBehavior = append ? ContentOutputBehavior.Append :
                    force ? ContentOutputBehavior.Overwrite :
                    ContentOutputBehavior.None;
                var outPath = generator.Generate(@in.Select(i => i.FullName), inputType, @out?.FullName, outputBehavior);
                Console.WriteLine($"Generated {outPath}");
            }));

    private static Command CreateExportCommand() =>
        new Command("export", "Export content from tandoku library")
        {
            new Argument<FileSystemInfo>("in", "Input file or path").ExistingOnly(),
            new Argument<FileInfo>("out", "Output file path") { Arity = ArgumentArity.ZeroOrOne }.LegalFilePathsOnly(),
            new Option<ExportFormat>("--format", "Target file format"),
        }.WithHandler(CommandHandler.Create(
            (FileInfo @in, FileInfo? @out, ExportFormat format) =>
            {
                var exporter = new Exporter();
                var outPath = exporter.Export(@in.FullName, @out?.FullName, format);
                Console.WriteLine($"Exported {outPath}");
            }));

    private static Command CreateTokenizeCommand() =>
        new Command("tokenize", "Tokenize text content")
        {
            new Argument<FileInfo>("in", "Input file or path").ExistingOnly(),
        }.WithHandler(CommandHandler.Create(
            (FileInfo @in) =>
            {
                var processor = new TextProcessor();
                processor.Tokenize(@in.FullName);
                Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
            }));

    private static Command CreateTransformCommand() =>
        new Command("transform", "Apply transforms to text content")
        {
            new Argument<string>("transform-list", "Comma-separated list of transforms to apply"),
            new Argument<FileInfo>("in", "Input file or path").ExistingOnly(),
        }.WithHandler(CommandHandler.Create(
            (string transformList, FileInfo @in) =>
            {
                var transforms = transformList.Split(',')
                    .Select(s => Enum.Parse<ContentTransformKind>(s, ignoreCase: true));

                var processor = new TextProcessor();
                processor.Transform(@in.FullName, transforms);
                Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
            }));

    private static Command CreateComputeCommand() =>
        new Command("compute", "Compute statistics")
        {
            CreateComputeContentCommand(),
            CreateComputeAggregatesCommand(),
            CreateComputeAnalyticsCommand(),
        };

    private static Command CreateComputeContentCommand() =>
        new Command("content", "Compute statistics for content")
        {
            new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly(),
            new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path") { IsRequired = true }.LegalFilePathsOnly(),
        }.WithHandler(CommandHandler.Create(
            (FileSystemInfo[] @in, FileInfo @out) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeStats(@in, @out.FullName);
                Console.WriteLine($"Computed statistics written to {@out.FullName}");
            }));

    private static Command CreateComputeAggregatesCommand() =>
        new Command("aggregates", "Compute aggregate statistics")
        {
            new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly(),
            new Option<FileInfo>(new[] { "-o", "--out" }, "Output file path") { IsRequired = true }.LegalFilePathsOnly(),
        }.WithHandler(CommandHandler.Create(
            (FileSystemInfo[] @in, FileInfo @out) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeAggregates(@in, @out.FullName);
                Console.WriteLine($"Computed aggregates written to {@out.FullName}");
            }));

    private static Command CreateComputeAnalyticsCommand() =>
        new Command("analytics", "Compute analytics for a volume in context of corpus aggregates")
        {
            new Argument<FileSystemInfo[]>("in", "Input files or paths") { Arity = ArgumentArity.OneOrMore }.LegalFilePathsOnly(),
            new Option<FileInfo>(new[] { "-c", "--corpus" }, "Corpus aggregates path") { IsRequired = true }.LegalFilePathsOnly(),
        }.WithHandler(CommandHandler.Create(
            (FileSystemInfo[] @in, FileInfo corpus) =>
            {
                var processor = new StatsProcessor();
                processor.ComputeAnalytics(@in, corpus.FullName);
                Console.WriteLine($"Computed analytics written to input files");
            }));
}
