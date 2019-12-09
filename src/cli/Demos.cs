using System;
using System.CommandLine;
using System.CommandLine.DragonFruit;
using System.Reflection;

namespace BlueMarsh.Tandoku.CommandLine
{
    internal static class Demos
    {
        internal static Command CreateCommand()
        {
            return new Command("demo")
            {
                CreateDemoCommand("dictionary", nameof(RunDictionaryLookupDemo), "dict"),
                CreateDemoCommand("tokenize", nameof(RunTokenizerDemo), "token"),
            };
        }

        private static Command CreateDemoCommand(string name, string method, params string[] aliases)
        {
            var command = new Command(name);
            command.ConfigureFromMethod(typeof(Demos).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static));
            foreach (string alias in aliases)
                command.AddAlias(alias);
            return command;
        }

        private static void RunDictionaryLookupDemo()
        {
            DictionaryLookupDemo.Run();
        }

        private static void RunTokenizerDemo()
        {
            TokenizerDemo.Dump();
        }
    }
}
