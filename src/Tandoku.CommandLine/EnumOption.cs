namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

internal static partial class EnumOption
{
    internal static Option<T> Create<T>(string name, params string[] aliases) where T : struct, Enum =>
        CreateCore<T>(name, aliases);

    internal static Option<T> Create<T>(string name, string? description, params string[] aliases) where T : struct, Enum
    {
        var option = CreateCore<T>(name, aliases);
        option.Description = description;
        return option;
    }

    internal static Option<T?> CreateNullable<T>(string name, string? description, params string[] aliases) where T : struct, Enum
    {
        var option = new Option<T?>(name, aliases)
        {
            Description = description,
            // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
            CustomParser = result =>
            {
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                    return value;
                result.AddError($"Invalid value for {name}.");
                return null;
            },
        };
        option.AcceptOnlyFromAmong(GetAcceptedValues<T>());
        return option;
    }

    private static Option<T> CreateCore<T>(string name, string[] aliases) where T : struct, Enum
    {
        var option = new Option<T>(name, aliases)
        {
            // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
            CustomParser = result =>
            {
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                    return value;
                result.AddError($"Invalid value for {name}.");
                return default;
            },
        };
        option.AcceptOnlyFromAmong(GetAcceptedValues<T>());
        return option;
    }

    private static string[] GetAcceptedValues<T>() where T : struct, Enum =>
        Enum.GetNames<T>().Select(n => PascalCaseToKebabCase(n)).ToArray();

    private static string PascalCaseToKebabCase(string name) =>
        PascalCaseBoundary().Replace(name, "-$1").TrimStart('-').ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
