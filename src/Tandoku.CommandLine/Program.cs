namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Parsing;
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

            // Legacy commands
            CreateGenerateCommand(),
            CreateExportCommand(),
            CreateTokenizeCommand(),
            CreateTransformCommand(),
            CreateComputeCommand(),

            Demos.CreateCommand(),
        };
        rootCommand.Options.Add(this.jsonOutputOption);
#if DEBUG
        rootCommand.Directives.Add(this.debugDirective);
#endif
        return rootCommand;
    }

    // Legacy commands

    private static Command CreateGenerateCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in")
        {
            Description = "Input files or paths",
            Arity = ArgumentArity.OneOrMore
        }.AcceptLegalFilePathsOnly();
        var inputTypeOpt = new Option<ContentGeneratorInputType?>("--input-type", "-t")
        {
            Description = "Type of input (derived from extension if not specified)"
        };
        var outputOpt = new Option<FileInfo>("--out", "-o")
        {
            Description = "Output file path"
        }.AcceptLegalFilePathsOnly();
        var appendOpt = new Option<bool>("--append", "-a")
        {
            Description = "Append to existing content"
        };
        var forceOpt = new Option<bool>("--force", "--overwrite", "-f")
        {
            Description = "Overwrite existing content"
        };

        var command = new Command("generate", "Generate tandoku content from various input formats")
        {
            inputArg,
            inputTypeOpt,
            outputOpt,
            appendOpt,
            forceOpt,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg);
            var inputType = parseResult.GetValue(inputTypeOpt);
            var @out = parseResult.GetValue(outputOpt);
            var append = parseResult.GetValue(appendOpt);
            var force = parseResult.GetValue(forceOpt);

            var generator = new ContentGenerator();
            var outputBehavior = append ? ContentOutputBehavior.Append :
                force ? ContentOutputBehavior.Overwrite :
                ContentOutputBehavior.None;
            var outPath = generator.Generate(@in!.Select(i => i.FullName), inputType, @out?.FullName, outputBehavior);
            Console.WriteLine($"Generated {outPath}");
        });
        return command;
    }

    private static Command CreateExportCommand()
    {
        var inputArg = new Argument<FileSystemInfo>("in")
        {
            Description = "Input file or path"
        }.AcceptExistingOnly();
        var outputArg = new Argument<FileInfo>("out")
        {
            Description = "Output file path",
            Arity = ArgumentArity.ZeroOrOne
        }.AcceptLegalFilePathsOnly();
        var formatOpt = new Option<ExportFormat>("--format")
        {
            Description = "Target file format"
        };

        var command = new Command("export", "Export content from tandoku library")
        {
            inputArg,
            outputArg,
            formatOpt,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg);
            var @out = parseResult.GetValue(outputArg);
            var format = parseResult.GetValue(formatOpt);

            var exporter = new Exporter();
            var outPath = exporter.Export(@in!.FullName, @out?.FullName, format);
            Console.WriteLine($"Exported {outPath}");
        });
        return command;
    }

    private static Command CreateTokenizeCommand()
    {
        var inputArg = new Argument<FileInfo>("in") { Description = "Input file or path" }.AcceptExistingOnly();
        var command = new Command("tokenize", "Tokenize text content")
        {
            inputArg,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg)!;
            var processor = new TextProcessor();
            processor.Tokenize(@in.FullName);
            Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
        });
        return command;
    }

    private static Command CreateTransformCommand()
    {
        var transformListArg = new Argument<string>("transform-list") { Description = "Comma-separated list of transforms to apply" };
        var inputArg = new Argument<FileInfo>("in") { Description = "Input file or path" }.AcceptExistingOnly();
        var command = new Command("transform", "Apply transforms to text content")
        {
            transformListArg,
            inputArg,
        };
        command.SetAction(parseResult =>
        {
            var transformList = parseResult.GetValue(transformListArg)!;
            var @in = parseResult.GetValue(inputArg)!;

            var transforms = transformList.Split(',')
                .Select(s => Enum.Parse<ContentTransformKind>(s, ignoreCase: true));

            var processor = new TextProcessor();
            processor.Transform(@in.FullName, transforms);
            Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks in {@in.FullName}");
        });
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
        var inputArg = new Argument<FileSystemInfo[]>("in") { Description = "Input files or paths", Arity = ArgumentArity.OneOrMore }.AcceptLegalFilePathsOnly();
        var outputOpt = new Option<FileInfo>("--out", "-o") { Description = "Output file path", Required = true }.AcceptLegalFilePathsOnly();
        var command = new Command("content", "Compute statistics for content")
        {
            inputArg,
            outputOpt,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg)!;
            var @out = parseResult.GetValue(outputOpt)!;
            var processor = new StatsProcessor();
            processor.ComputeStats(@in, @out.FullName);
            Console.WriteLine($"Computed statistics written to {@out.FullName}");
        });
        return command;
    }

    private static Command CreateComputeAggregatesCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in") { Description = "Input files or paths", Arity = ArgumentArity.OneOrMore }.AcceptLegalFilePathsOnly();
        var outputOpt = new Option<FileInfo>("--out", "-o") { Description = "Output file path", Required = true }.AcceptLegalFilePathsOnly();
        var command = new Command("aggregates", "Compute aggregate statistics")
        {
            inputArg,
            outputOpt,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg)!;
            var @out = parseResult.GetValue(outputOpt)!;
            var processor = new StatsProcessor();
            processor.ComputeAggregates(@in, @out.FullName);
            Console.WriteLine($"Computed aggregates written to {@out.FullName}");
        });
        return command;
    }

    private static Command CreateComputeAnalyticsCommand()
    {
        var inputArg = new Argument<FileSystemInfo[]>("in") { Description = "Input files or paths", Arity = ArgumentArity.OneOrMore }.AcceptLegalFilePathsOnly();
        var corpusOpt = new Option<FileInfo>("--corpus", "-c") { Description = "Corpus aggregates path", Required = true }.AcceptLegalFilePathsOnly();
        var command = new Command("analytics", "Compute analytics for a volume in context of corpus aggregates")
        {
            inputArg,
            corpusOpt,
        };
        command.SetAction(parseResult =>
        {
            var @in = parseResult.GetValue(inputArg)!;
            var corpus = parseResult.GetValue(corpusOpt)!;
            var processor = new StatsProcessor();
            processor.ComputeAnalytics(@in, corpus.FullName);
            Console.WriteLine($"Computed analytics written to input files");
        });
        return command;
    }
}
