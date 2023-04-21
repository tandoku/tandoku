namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed record LibraryVersion
{
    public static LibraryVersion Latest { get; } = new LibraryVersion(new Version(0, 1, 0));

    public Version Version { get; init; }

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

    public async Task WriteToAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await writer.WriteAsync(this.ToString());
    }

    public override string ToString()
    {
        return this.Version.ToString();
    }
}
