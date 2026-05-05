namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.RegularExpressions;

// TODO - Change this from static helper class to EnumOption<T> / NullableEnumOption<T> classes derived from Option<T>
// This will enable callers to set Description and other properties with object initializer syntax
internal static partial class EnumOption
{
    internal static Option<T> Create<T>(string name, params string[] aliases) where T : struct, Enum
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

    internal static Option<T?> CreateNullable<T>(string name, params string[] aliases) where T : struct, Enum
    {
        var option = new Option<T?>(name, aliases)
        {
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

    private static string[] GetAcceptedValues<T>() where T : struct, Enum =>
        [.. Enum.GetNames<T>().Select(n => PascalCaseToKebabCase(n))];

    private static string PascalCaseToKebabCase(string name) =>
        PascalCaseBoundary().Replace(name, "-$1").TrimStart('-').ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
