namespace Tandoku.Volume;

using System;
using Tandoku.Packaging;

public sealed record VolumeVersion : IPackageVersion
{
    public static VolumeVersion Latest { get; } = new VolumeVersion(new Version(0, 1, 0));

    public Version Version { get; }

    private VolumeVersion(Version version)
    {
        this.Version = version;
    }
}
