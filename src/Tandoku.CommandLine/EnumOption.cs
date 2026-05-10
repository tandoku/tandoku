namespace Tandoku.CommandLine;

using System.CommandLine;
using System.Text.RegularExpressions;

internal static partial class EnumOption
{
    internal static string[] GetAcceptedValues<T>() where T : struct, Enum =>
        [.. Enum.GetNames<T>().Select(n => PascalCaseToKebabCase(n))];

    private static string PascalCaseToKebabCase(string name) =>
        PascalCaseBoundary().Replace(name, "-$1").TrimStart('-').ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}

internal class EnumOption<T> : Option<T> where T : struct, Enum
{
    public EnumOption(string name, params string[] aliases)
        : base(name, aliases)
    {
        // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
        this.CustomParser = result =>
        {
            if (result.Tokens.Count == 1 &&
                Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                return value;
            result.AddError($"Invalid value for {name}.");
            return default;
        };
        this.AcceptOnlyFromAmong(EnumOption.GetAcceptedValues<T>());
    }
}

internal class NullableEnumOption<T> : Option<T?> where T : struct, Enum
{
    public NullableEnumOption(string name, params string[] aliases)
        : base(name, aliases)
    {
        // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
        this.CustomParser = result =>
        {
            if (result.Tokens.Count == 1 &&
                Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                return value;
            result.AddError($"Invalid value for {name}.");
            return null;
        };
        this.AcceptOnlyFromAmong(EnumOption.GetAcceptedValues<T>());
    }
}
