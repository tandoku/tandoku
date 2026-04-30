namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Parsing;

internal static class EnumOption
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
            CustomParser = result =>
            {
                // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                    return value;
                result.AddError($"Invalid value for {name}.");
                return null;
            },
        };
        return option;
    }

    private static Option<T> CreateCore<T>(string name, string[] aliases) where T : struct, Enum
    {
        var option = new Option<T>(name, aliases)
        {
            CustomParser = result =>
            {
                // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value))
                    return value;
                result.AddError($"Invalid value for {name}.");
                return default;
            },
        };
        return option;
    }
}
