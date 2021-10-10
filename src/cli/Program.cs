using System;
using System.CommandLine;
using System.CommandLine.DragonFruit;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;

namespace BlueMarsh.Tandoku.CommandLine
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                //Demos.CreateCommand(),
                //CreateCommand("import", nameof(Import)),
                CreateImportCommand(),
                //CreateCommand("export", nameof(Export)),
                //CreateCommand("tokenize", nameof(Tokenize)),
            };
            rootCommand.Description = "Command-line interface for tandoku.";

            rootCommand.Invoke(args);
        }

        private static Command CreateImportCommand()
        {
            var cmd =
                new Command("import", "Import content into tandoku library")
                {
                    new Option<FileSystemInfo>("--in", "Input file or path") { IsRequired = true }.ExistingOnly(),
                    new Option<FileInfo>("--out", "Output file path").LegalFilePathsOnly(),
                    new Option<bool>("--images"),
                };
            cmd.Handler = CommandHandler.Create(
                (FileSystemInfo @in, FileInfo @out, bool images) =>
                {
                    var importer = new Importer();
                    var outPath = importer.Import(@in.FullName, @out?.FullName, images);
                    Console.WriteLine($"Imported {outPath}");
                });
            return cmd;
        }

        private static Command CreateCommand(string name, string method, params string[] aliases)
        {
            var command = new Command(name);
            command.ConfigureFromMethod(typeof(Program).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static));
            foreach (string alias in aliases)
                command.AddAlias(alias);
            return command;
        }

        //private static void Import(FileInfo? file = null, bool images = false)
        //{
        //    if (file == null && !images)
        //    {
        //        Console.WriteLine("Expected either <file> or --images argument");
        //        return;
        //    }

        //    var importer = new Importer();
        //    var outPath = importer.Import(file?.FullName ?? ".", images);
        //    Console.WriteLine($"Imported {outPath}");
        //}

        private static void Export(FileInfo file, ExportFormat format)
        {
            var exporter = new Exporter();
            var outPath = exporter.Export(file.FullName, format);
            Console.WriteLine($"Exported {outPath}");
        }

        private static void Tokenize(FileInfo file)
        {
            var processor = new TextProcessor();
            processor.Tokenize(file.FullName);
            Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks");
        }
    }
}
