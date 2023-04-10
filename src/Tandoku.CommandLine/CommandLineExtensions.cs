namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;

internal static class CommandLineExtensions
{
    internal static Command WithHandler(this Command command, ICommandHandler handler)
    {
        command.Handler = handler;
        return command;
    }

    internal static void Write(this IConsole console, string value) => console.Out.Write(value);
    internal static void WriteLine(this IConsole console) => console.Out.WriteLine();
    internal static void WriteLine(this IConsole console, string value) => console.Out.WriteLine(value);
}
