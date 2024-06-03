namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

internal interface ICommandBinder
{
    void AddToCommand(Command command);
}

internal static class CommandLineExtensions
{
    private const string NullOutputString = "<none>";

    // TODO: remove when no longer used
    internal static Command WithHandler(this Command command, ICommandHandler handler)
    {
        command.Handler = handler;
        return command;
    }

    internal static void Add(this Command command, ICommandBinder binder) => binder.AddToCommand(command);

    internal static void Write(this IConsole console, string value) => console.Out.Write(value);
    internal static void WriteLine(this IConsole console) => console.Out.WriteLine();
    internal static void WriteLine(this IConsole console, string value) => console.Out.WriteLine(value);

    internal static void WriteJsonOutput(this IConsole console, object obj)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        console.WriteLine(JsonSerializer.Serialize(obj, options));
    }

    internal static string ToOutputString(this string? s)
    {
        return s ?? NullOutputString;
    }

    internal static string ToOutputString(this IEnumerable<string>? set)
    {
        return set is not null && set.Any() ? string.Join(", ", set) : NullOutputString;
    }
}
