namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;

internal interface ICommandBinder<T>
{
    void AddToCommand(Command command);
    T Resolve(ParseResult parseResult);
}

internal static class CommandLineExtensions
{
    private const string NullOutputString = "<none>";

    internal static void Add<T>(this Command command, ICommandBinder<T> binder) => binder.AddToCommand(command);

    internal static T GetValue<T>(this ParseResult parseResult, ICommandBinder<T> binder) => binder.Resolve(parseResult);

    internal static (T1, T2) GetValues<T1, T2>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2));

    internal static (T1, T2) GetValues<T1, T2>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        ICommandBinder<T2> binder2) =>
        (binder1.Resolve(parseResult), binder2.Resolve(parseResult));

    internal static (T1, T2, T3) GetValues<T1, T2, T3>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        Option<T3> option3) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), parseResult.GetValue(option3));

    internal static (T1, T2, T3) GetValues<T1, T2, T3>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        ICommandBinder<T3> binder3) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), binder3.Resolve(parseResult));

    internal static (T1, T2, T3, T4) GetValues<T1, T2, T3, T4>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        Option<T3> option3,
        Option<T4> option4) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), parseResult.GetValue(option3), parseResult.GetValue(option4));

    internal static (T1, T2, T3, T4) GetValues<T1, T2, T3, T4>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        Option<T3> option3,
        ICommandBinder<T4> binder4) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), parseResult.GetValue(option3), binder4.Resolve(parseResult));

    internal static (T1, T2, T3, T4, T5) GetValues<T1, T2, T3, T4, T5>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        Option<T3> option3,
        Option<T4> option4,
        Option<T5> option5) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), parseResult.GetValue(option3), parseResult.GetValue(option4), parseResult.GetValue(option5));

    internal static (T1, T2, T3, T4, T5) GetValues<T1, T2, T3, T4, T5>(
        this ParseResult parseResult,
        ICommandBinder<T1> binder1,
        Option<T2> option2,
        Option<T3> option3,
        Option<T4> option4,
        ICommandBinder<T5> binder5) =>
        (binder1.Resolve(parseResult), parseResult.GetValue(option2), parseResult.GetValue(option3), parseResult.GetValue(option4), binder5.Resolve(parseResult));

    internal static void WriteJsonOutput(this TextWriter writer, object obj)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        writer.WriteLine(JsonSerializer.Serialize(obj, options));
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
