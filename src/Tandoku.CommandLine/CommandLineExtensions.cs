namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;

// TODO - consider renaming this, post-System.CommandLine 2.0 stable refactoring it's really just a helper
// for collecting multiple common arguments/options rather than a "Binder" anymore
// Maybe this should really be a "collection" that exposes public properties for the individual arguments/options,
// and implements IEnumerable on the argument/option union type to enable adding the collection to a command.
internal interface ICommandBinder<T>
{
    // TODO - consider replacing AddToCommand with a method that returns an IEnumerable of Argument/Option union
    void AddToCommand(Command command);
    T Resolve(ParseResult parseResult);
}

// TODO: use union type after upgrading to .NET 11
// Issue - this doesn't work as desired currently because the implicit conversions do not participate in type inference;
// unfortunately unions in .NET 11 Preview 3 have the same problem.
// Raised for discussion in https://github.com/dotnet/csharplang/discussions/10164
internal readonly struct Parameter<T>
{
    private readonly object? value;

    public Parameter(Argument<T> argument) => this.value = argument;
    public Parameter(Option<T> option) => this.value = option;
    public Parameter(ICommandBinder<T> binder) => this.value = binder;

    public T? GetValue(ParseResult parseResult) => this.value switch
    {
        Argument<T> argument => parseResult.GetValue(argument),
        Option<T> option => parseResult.GetValue(option),
        ICommandBinder<T> binder => binder.Resolve(parseResult),
        _ => throw new InvalidOperationException($"Cannot call {nameof(GetValue)} on a default instance of {nameof(Parameter<>)}."),
    };

    public static implicit operator Parameter<T>(Argument<T> argument) => new(argument);
    public static implicit operator Parameter<T>(Option<T> option) => new(option);
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

    internal static (T1?, T2?) GetValues<T1, T2>(
        this ParseResult parseResult,
        Parameter<T1> param1,
        Parameter<T2> param2) =>
        (param1.GetValue(parseResult), param2.GetValue(parseResult));

    internal static (T1?, T2?, T3?) GetValues<T1, T2, T3>(
        this ParseResult parseResult,
        Parameter<T1> param1,
        Parameter<T2> param2,
        Parameter<T3> param3) =>
        (param1.GetValue(parseResult), param2.GetValue(parseResult), param3.GetValue(parseResult));

    internal static (T1?, T2?, T3?, T4?) GetValues<T1, T2, T3, T4>(
        this ParseResult parseResult,
        Parameter<T1> param1,
        Parameter<T2> param2,
        Parameter<T3> param3,
        Parameter<T4> param4) =>
        (param1.GetValue(parseResult), param2.GetValue(parseResult), param3.GetValue(parseResult), param4.GetValue(parseResult));

    internal static (T1?, T2?, T3?, T4?, T5?) GetValues<T1, T2, T3, T4, T5>(
        this ParseResult parseResult,
        Parameter<T1> param1,
        Parameter<T2> param2,
        Parameter<T3> param3,
        Parameter<T4> param4,
        Parameter<T5> param5) =>
        (param1.GetValue(parseResult), param2.GetValue(parseResult), param3.GetValue(parseResult), param4.GetValue(parseResult), param5.GetValue(parseResult));

    internal static (T1?, T2?, T3?, T4?, T5?, T6?) GetValues<T1, T2, T3, T4, T5, T6>(
        this ParseResult parseResult,
        Parameter<T1> param1,
        Parameter<T2> param2,
        Parameter<T3> param3,
        Parameter<T4> param4,
        Parameter<T5> param5,
        Parameter<T6> param6) =>
        (param1.GetValue(parseResult), param2.GetValue(parseResult), param3.GetValue(parseResult), param4.GetValue(parseResult), param5.GetValue(parseResult), param6.GetValue(parseResult));

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
