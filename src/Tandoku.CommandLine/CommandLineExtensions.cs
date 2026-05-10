namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

// TODO - consider renaming this, post-System.CommandLine 2.0 stable refactoring it's really just a helper
// for collecting multiple common arguments/options rather than a "Binder" anymore
// Maybe this should really be a "collection" that exposes public properties for the individual arguments/options,
// and implements IEnumerable on the argument/option union type to enable adding the collection to a command.
// *** Check out LibraryBinder and VolumeBinder, these are much more involved than the InputOutputPathArgsBinder.
//     Figure out if their functionality can be implemented using DefaultValueFactory and/or Validators.
//     Nullability is an interesting question - ParseResult.GetValue always returns a nullable value.
//     GetRequiredValue/GetRequiredValues methods could ensure that the parameter is Required and return non-nullable value.
//     But library/volume location involve non-required options (and potentially multiple options in the future) so
//     how should we model returning non-nullable values from these? Maybe this warrants a wrapper object/concept
//     (like "binder") to provide this additional semantics?
//     ==> no, this is too much coupling. The resolve library/volume logic should be separate from the parameters;
//         parameters should be inputs to that function. We could introduce "parameter bundles" if really needed
//         but for now let's just have helper methods to create common parameters, GetValues/GetRequiredValues to
//         reduce boilerplate for collecting parameter values and functions for resolving library/volume.
internal interface ICommandBinder<T>
{
    // TODO - consider replacing AddToCommand with a method that returns an IEnumerable of Argument/Option union
    void AddToCommand(Command command);
    T Resolve(ParseResult parseResult);
}

// TODO: use union type after upgrading to .NET 11
internal readonly struct Parameter<T>
{
    private readonly object? value;

    public Parameter(Argument<T> argument) => this.value = argument;
    public Parameter(Option<T> option) => this.value = option;

    public T GetRequiredValue(ParseResult parseResult) => this.value switch
    {
        Argument<T> argument => parseResult.GetRequiredValue(argument),
        Option<T> option => parseResult.GetRequiredValue(option),
        _ => throw new InvalidOperationException($"Cannot call {nameof(GetValue)} on a default instance of {nameof(Parameter<>)}."),
    };

    public T? GetValue(ParseResult parseResult) => this.value switch
    {
        Argument<T> argument => parseResult.GetValue(argument),
        Option<T> option => parseResult.GetValue(option),
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
