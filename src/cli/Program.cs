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
                Demos.CreateCommand(),
                CreateCommand("tokenize", nameof(Tokenize)),
            };

            rootCommand.Invoke(args);
        }

        private static Command CreateCommand(string name, string method, params string[] aliases)
        {
            var command = new Command(name);
            command.ConfigureFromMethod(typeof(Program).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static));
            foreach (string alias in aliases)
                command.AddAlias(alias);
            return command;
        }

        private static void Tokenize(FileInfo file)
        {
            var processor = new TextProcessor();
            processor.Tokenize(file.FullName);
            Console.WriteLine($"Processed {processor.ProcessedBlocksCount} text blocks");
        }
    }
}
