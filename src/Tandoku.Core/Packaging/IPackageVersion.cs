namespace Tandoku.Packaging;

using System.IO.Abstractions;

internal interface IPackageVersion
{
    Version Version { get; }

    //public static async Task<LibraryVersion> ReadFromAsync(IFileInfo file)
    //{
    //    using var reader = file.OpenText();
    //    var s = await reader.ReadToEndAsync();
    //    return Version.TryParse(s, out var version) && version.Equals(Latest.Version) ?
    //        Latest :
    //        throw new InvalidDataException("The specified version is not recognized.");
    //}

    public async Task WriteToAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await writer.WriteAsync(this.Version.ToString());
    }
}
