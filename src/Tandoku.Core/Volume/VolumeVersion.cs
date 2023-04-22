namespace Tandoku.Volume;

using System;
using System.Diagnostics.CodeAnalysis;
using Tandoku.Packaging;

public sealed record VolumeVersion : IPackageVersion<VolumeVersion>
{
    public static VolumeVersion Latest { get; } = new VolumeVersion(new Version(0, 1, 0));

    public Version Version { get; }

    private VolumeVersion(Version version)
    {
        this.Version = version;
    }

    public static bool TryGet(Version version, [NotNullWhen(true)] out VolumeVersion? resolvedVersion)
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
