namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Parsing;

internal static class EnumOption
{
    internal static Option<T> Create<T>(string name, string? description = null) where T : struct, Enum =>
        new Option<T>(name, EnumParseArgument<T>.Instance, description: description);

    internal static Option<T> Create<T>(string[] aliases, string? description = null) where T : struct, Enum =>
        new Option<T>(aliases, EnumParseArgument<T>.Instance, description: description);

    internal static Option<T?> CreateNullable<T>(string name, string? description = null) where T : struct, Enum =>
        new Option<T?>(name, NullableEnumParseArgument<T>.Instance, description: description);

    internal static Option<T?> CreateNullable<T>(string[] aliases, string? description = null) where T : struct, Enum =>
        new Option<T?>(aliases, NullableEnumParseArgument<T>.Instance, description: description);

    private static class EnumParseArgument<T>
        where T : struct, Enum
    {
        // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
        public static ParseArgument<T> Instance = (ArgumentResult result) =>
            (result.Tokens.Count == 1 &&
             Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value)) ?
            value :
            throw new ArgumentOutOfRangeException();
    }

    private static class NullableEnumParseArgument<T>
        where T : struct, Enum
    {
        // TODO - use EnumMemberAttribute.Value instead of removing "-" chars
        // and share logic with above ??
        public static ParseArgument<T?> Instance = (ArgumentResult result) =>
            (result.Tokens.Count == 1 &&
             Enum.TryParse<T>(result.Tokens[0].Value.Replace("-", ""), ignoreCase: true, out var value)) ?
            value :
            throw new ArgumentOutOfRangeException();
    }
}
