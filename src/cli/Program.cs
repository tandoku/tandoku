namespace BlueMarsh.Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Invocation;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return new RootCommand("Command-line interface for tandoku")
        {
            CreateImportCommand(),
            CreateExportCommand(),
            CreateTokenizeCommand(),

            Demos.CreateCommand(),
        }.Invoke(args);
    }

    private static Command CreateImportCommand() =>
        new Command("import", "Import content into tandoku library")
        {
            new Argument<FileSystemInfo>("in", "Input file or path").ExistingOnly(),
            new Argument<FileInfo>("out", "Output file path") { Arity = ArgumentArity.ZeroOrOne }.LegalFilePathsOnly(),
            new Option<bool>("--images", "Import images from the specified directory"),
        }.WithHandler(CommandHandler.Create(
            (FileSystemInfo @in, FileInfo? @out, bool images) =>
            {
                var importer = new Importer();
                var outPath = importer.Import(@in.FullName, @out?.FullName, images);
                Console.WriteLine($"Imported {outPath}");
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
                Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks");
            }));
}
