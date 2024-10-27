namespace Tandoku.CommandLine;

using System.CommandLine;

internal static class Demos
{
    internal static Command CreateCommand()
    {
        return new Command("demo")
        {
            CreateDemoCommand("dictionary", RunDictionaryLookupDemo, "dict"),
            CreateDemoCommand("tokenize", new Option<bool>("dict"), RunTokenizerDemo, "token"),
            CreateDemoCommand("compile", RunDictionaryCompiler),
        };
    }

    private static Command CreateDemoCommand(string name, Action handler, params string[] aliases)
    {
        var command = CreateDemoCommandCore(name, aliases);
        command.SetHandler(handler);
        return command;
    }

    private static Command CreateDemoCommand(string name, Option<bool> option, Action<bool> handler, params string[] aliases)
    {
        var command = CreateDemoCommandCore(name, aliases);
        command.AddOption(option);
        command.SetHandler(handler, option);
        return command;
    }

    private static Command CreateDemoCommandCore(string name, string[] aliases)
    {
        var command = new Command(name);
        foreach (var alias in aliases)
            command.AddAlias(alias);
        return command;
    }

    private static void RunDictionaryCompiler()
    {
        DictionaryCompiler.Compile();
    }

    private static void RunDictionaryLookupDemo()
    {
        DictionaryLookupDemo.Run();
    }

    private static void RunTokenizerDemo(bool dict)
    {
        TokenizerDemo.Dump(dict);
    }
}
