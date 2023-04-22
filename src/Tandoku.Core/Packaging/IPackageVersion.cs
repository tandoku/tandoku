namespace Tandoku.Packaging;

using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;

internal interface IPackageVersion<TSelf>
    where TSelf : class, IPackageVersion<TSelf>
{
    Version Version { get; }

    static abstract bool TryGet(Version version, [NotNullWhen(true)] out TSelf? resolvedVersion);

    static async Task<TSelf> ReadFromAsync(IFileInfo file)
    {
        using var reader = file.OpenText();
        var s = await reader.ReadToEndAsync();
        return Version.TryParse(s, out var version) && TSelf.TryGet(version, out var resolvedVersion) ?
            resolvedVersion :
            throw new InvalidDataException("The specified version is not recognized.");
    }

    async Task WriteToAsync(IFileInfo file)
    {
        // Note: this method must be 'async' so writer is not disposed prematurely
        using var writer = file.CreateText();
        await writer.WriteAsync(this.Version.ToString());
    }
}
