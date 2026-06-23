namespace Tandoku.CommandLine;

using System.CommandLine;
using Scriban.Runtime;
using Tandoku.Markdown;

public sealed partial class Program
{
    private Command CreateMarkdownCommand() =>
        new("markdown", "Commands for working with markdown derived from tandoku content")
        {
            this.CreateMarkdownExportCommand(),
        };

    private Command CreateMarkdownExportCommand()
    {
        var inputPathArgument = ArgumentFactory.InputPath();
        var outputPathArgument = new Argument<string>("output-path")
        {
            Description = "Path of output directory or, when --combine is set, an output .md file",
            Arity = ArgumentArity.ExactlyOne,
        }.AcceptLegalFilePathsOnly();
        var combineOption = new Option<bool>("--combine")
        {
            Description = "Combine all input content files into a single markdown file",
        };
        var noBlockHeadingsOption = new Option<bool>("--no-block-headings")
        {
            Description = "Do not render per-block headings from notes/resources/timecodes",
        };
        var optionOption = new Option<string[]>("--option")
        {
            Description = "Custom template option in the form key=value; may be specified multiple times",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
        };
        var rubyOption = new Option<MarkdownRubyBehavior>("--ruby")
        {
            Description = "How ruby annotations should be rendered",
            DefaultValueFactory = _ => MarkdownRubyBehavior.None,
        };
        var refBehaviorOption = new Option<MarkdownReferenceBehavior>("--ref-behavior")
        {
            Description = "How reference text should be rendered",
            DefaultValueFactory = _ => MarkdownReferenceBehavior.None,
        };
        var refLabelsOption = new Option<MarkdownReferenceLabels>("--ref-labels")
        {
            Description = "Whether to render labels for references",
            DefaultValueFactory = _ => MarkdownReferenceLabels.Default,
        };
        var quirksOption = new Option<MarkdownQuirks>("--quirks")
        {
            Description = "Reader-specific output quirks to apply",
            DefaultValueFactory = _ => MarkdownQuirks.None,
        };
        var templateOption = new Option<FileInfo?>("--template")
        {
            Description = "Path to a custom Scriban template file to use for rendering content",
        }.AcceptLegalFilePathsOnly();
        var blockLimitOption = new Option<int?>("--block-limit")
        {
            Description = "Maximum number of blocks the template may iterate over per content file",
        };

        var command = new Command("export", "Exports tandoku content to markdown")
        {
            inputPathArgument,
            outputPathArgument,
            combineOption,
            noBlockHeadingsOption,
            optionOption,
            rubyOption,
            refBehaviorOption,
            refLabelsOption,
            quirksOption,
            templateOption,
            blockLimitOption,
        };

        // TODO - port -OutputPrefix from the PS script, or (preferred) introduce a general-purpose
        // build task that applies a prefix to a set of files instead of baking it into export.
        // TODO - integrate with version control: stage modified files in the output path so the user
        // can run the command and diff against staged files. Add an opt-out switch for skipping VC.
        command.SetAction(async (parseResult, ct) =>
        {
            var inputPath = parseResult.GetRequiredValue(inputPathArgument);
            var outputPath = parseResult.GetRequiredValue(outputPathArgument);
            var settings = new MarkdownExportSettings
            {
                Combine = parseResult.GetValue(combineOption),
                NoBlockHeadings = parseResult.GetValue(noBlockHeadingsOption),
                RubyBehavior = parseResult.GetValue(rubyOption),
                ReferenceBehavior = parseResult.GetValue(refBehaviorOption),
                ReferenceLabels = parseResult.GetValue(refLabelsOption),
                Quirks = parseResult.GetValue(quirksOption),
                TemplatePath = parseResult.GetValue(templateOption)?.FullName,
                BlockLimit = parseResult.GetValue(blockLimitOption),
                CustomOptions = ParseCustomOptions(parseResult.GetValue(optionOption)),
            };

            var exporter = new MarkdownExporter(settings, this.fileSystem);
            var written = await exporter.ExportAsync(inputPath.FullName, outputPath);
            foreach (var file in written)
                this.output.WriteLine($"Wrote {file}");
        });

        return command;
    }

    private static ScriptObject ParseCustomOptions(string[]? options)
    {
        var customOptions = new ScriptObject();
        if (options is null)
            return customOptions;

        foreach (var option in options)
        {
            var separatorIndex = option.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
                throw new ArgumentException($"Invalid --option value '{option}'; expected key=value.");

            var key = option[..separatorIndex];
            var value = option[(separatorIndex + 1)..];
            customOptions[key] = CoerceOptionValue(value);
        }

        return customOptions;
    }

    private static object CoerceOptionValue(string value) =>
        bool.TryParse(value, out var boolValue) ? boolValue :
        long.TryParse(value, out var longValue) ? longValue :
        double.TryParse(value, out var doubleValue) ? doubleValue :
        value;
}
