namespace Tandoku.Tests;

internal static class TypeExtensions
{
    internal static Stream GetManifestResourceStream(this Type type, string name) =>
        type.Assembly.GetManifestResourceStream(type, name) ??
        throw new ArgumentException("Missing manifest resource stream: {name}", nameof(name));
}
