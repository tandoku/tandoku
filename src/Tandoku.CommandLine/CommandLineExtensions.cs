namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Encodings.Web;
using System.Text.Json;

internal interface ICommandBinder
{
    void AddToCommand(Command command);
}

internal static class CommandLineExtensions
{
    private const string NullOutputString = "<none>";

    internal static void Add(this Command command, ICommandBinder binder) => binder.AddToCommand(command);

    internal static void Write(this TextWriter writer, string value) => writer.Write(value);
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

    // Path validation extensions

    internal static Argument<T> LegalFilePathsOnly<T>(this Argument<T> argument)
    {
        argument.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var path = token.Value;
                if (HasInvalidPathChars(path))
                {
                    result.AddError($"Illegal characters in path '{path}'.");
                }
            }
        });
        return argument;
    }

    internal static Option<T> LegalFilePathsOnly<T>(this Option<T> option)
    {
        option.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var path = token.Value;
                if (HasInvalidPathChars(path))
                {
                    result.AddError($"Illegal characters in path '{path}'.");
                }
            }
        });
        return option;
    }

    internal static Argument<T> ExistingOnly<T>(this Argument<T> argument)
    {
        argument.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var path = token.Value;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    result.AddError($"File or directory does not exist: '{path}'.");
                }
            }
        });
        return argument;
    }

    internal static Option<T> ExistingOnly<T>(this Option<T> option)
    {
        option.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var path = token.Value;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    result.AddError($"File or directory does not exist: '{path}'.");
                }
            }
        });
        return option;
    }

    internal static Option<T> LegalFileNamesOnly<T>(this Option<T> option)
    {
        option.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var name = token.Value;
                if (HasInvalidFileNameChars(name))
                {
                    result.AddError($"Illegal characters in file name '{name}'.");
                }
            }
        });
        return option;
    }

    private static bool HasInvalidPathChars(string path) =>
        path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;

    private static bool HasInvalidFileNameChars(string name) =>
        name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
}
