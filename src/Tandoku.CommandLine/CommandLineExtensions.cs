namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spectre.Console;

internal interface ICommandBinder
{
    void AddToCommand(Command command);
}

internal static class CommandLineExtensions
{
    private const string NullOutputString = "<none>";

    internal static void Add(this Command command, ICommandBinder binder) => binder.AddToCommand(command);

    internal static void WriteJsonOutput(this IAnsiConsole console, object obj)
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
