namespace Tandoku.CommandLine;

using System.CommandLine;
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
        // TODO - revisit naming/semantics; this is really "do not promote images to headings".
        // Consider making this the default and adding a --heading-per-block (or similar) switch instead.
        var noHeadingsOption = new Option<bool>("--no-headings")
        {
            Description = "Do not promote per-block notes/resources to headings",
        };
        var keepTogetherOption = new Option<bool>("--keep-together")
        {
            Description = "Wrap each block in a keep-together div",
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

        var command = new Command("export", "Exports tandoku content to markdown")
        {
            inputPathArgument,
            outputPathArgument,
            combineOption,
            noHeadingsOption,
            keepTogetherOption,
            rubyOption,
            refBehaviorOption,
            refLabelsOption,
            quirksOption,
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
                NoHeadings = parseResult.GetValue(noHeadingsOption),
                KeepTogether = parseResult.GetValue(keepTogetherOption),
                RubyBehavior = parseResult.GetValue(rubyOption),
                ReferenceBehavior = parseResult.GetValue(refBehaviorOption),
                ReferenceLabels = parseResult.GetValue(refLabelsOption),
                Quirks = parseResult.GetValue(quirksOption),
            };

            var exporter = new MarkdownExporter(settings, this.fileSystem);
            var written = await exporter.ExportAsync(inputPath.FullName, outputPath);
            foreach (var file in written)
                this.output.WriteLine($"Wrote {file}");
        });

        return command;
    }
}
