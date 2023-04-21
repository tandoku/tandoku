namespace Tandoku.Library;

using System.IO.Abstractions;
using Tandoku.Packaging;

public sealed record LibraryVersion : IPackageVersion
{
    public static LibraryVersion Latest { get; } = new LibraryVersion(new Version(0, 1, 0));

    public Version Version { get; }

    private LibraryVersion(Version version)
    {
        this.Version = version;
    }

    public static async Task<LibraryVersion> ReadFromAsync(IFileInfo file)
    {
        using var reader = file.OpenText();
        var s = await reader.ReadToEndAsync();
        return Version.TryParse(s, out var version) && version.Equals(Latest.Version) ?
            Latest :
            throw new InvalidDataException("The specified version is not recognized.");
    }
}
