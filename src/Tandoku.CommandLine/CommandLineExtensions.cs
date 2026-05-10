namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;

internal interface ICommandBinder<T>
{
    // TODO - consider replacing AddToCommand with a method that returns an IEnumerable of Argument/Option union
    void AddToCommand(Command command);
    T Resolve(ParseResult parseResult);
}

internal static class CommandLineExtensions
{
    private const string NullOutputString = "<none>";
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal static void Add<T>(this Command command, ICommandBinder<T> binder) => binder.AddToCommand(command);
    internal static T GetValue<T>(this ParseResult parseResult, ICommandBinder<T> binder) => binder.Resolve(parseResult);

    // TODO - consider unifying these Argument/Option overloads with a Parameter<T> union type
    // Only worth doing this if case-to-union conversions participate in type inference
    // (as of .NET 11 Preview 3 they do not) - see https://github.com/dotnet/csharplang/discussions/9663#discussioncomment-16811915

    internal static (T1, T2) GetRequiredValues<T1, T2>(
        this ParseResult parseResult,
        Argument<T1> arg1,
        Argument<T2> arg2) =>
        (parseResult.GetRequiredValue(arg1), parseResult.GetRequiredValue(arg2));

    internal static (T1?, T2?) GetValues<T1, T2>(
        this ParseResult parseResult,
        Option<T1> option1,
        Option<T2> option2) =>
        (parseResult.GetValue(option1), parseResult.GetValue(option2));

    internal static (T1?, T2?, T3?) GetValues<T1, T2, T3>(
        this ParseResult parseResult,
        Option<T1> option1,
        Option<T2> option2,
        Option<T3> option3) =>
        (parseResult.GetValue(option1), parseResult.GetValue(option2), parseResult.GetValue(option3));

    internal static void WriteJsonOutput(this TextWriter writer, object obj)
    {
        writer.WriteLine(JsonSerializer.Serialize(obj, jsonSerializerOptions));
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
