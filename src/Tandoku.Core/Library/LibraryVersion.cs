namespace Tandoku.Library;

using System.Diagnostics.CodeAnalysis;
using Tandoku.Packaging;

public sealed record LibraryVersion : IPackageVersion<LibraryVersion>
{
    public static LibraryVersion Latest { get; } = new LibraryVersion(new Version(0, 1, 0));

    public Version Version { get; }

    private LibraryVersion(Version version)
    {
        this.Version = version;
    }

    public static bool TryGet(Version version, [NotNullWhen(true)] out LibraryVersion? resolvedVersion)
    {
        if (version.Equals(Latest.Version))
        {
            resolvedVersion = Latest;
            return true;
        }
        resolvedVersion = default;
        return false;
    }
}
