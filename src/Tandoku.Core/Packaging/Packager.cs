namespace Tandoku.Packaging;

using System.IO.Abstractions;

internal sealed class Packager<TVersion>
    where TVersion : class, IPackageVersion<TVersion>
{
    private const string VersionFileName = "version";

    private readonly string packageTypeName;
    private readonly string metadataDirectoryName;

    internal Packager(string packageTypeName)
    {
        this.packageTypeName = packageTypeName;
        this.metadataDirectoryName = ".tandoku-" + packageTypeName;
    }

    internal async Task InitializePackageAsync(IDirectoryInfo directory, TVersion version, bool force)
    {
        if (directory.Exists)
        {
            if (!force && directory.EnumerateFileSystemInfos().Any())
                throw new ArgumentException("The specified directory is not empty and force is not specified.");
        }
        else
        {
            // Note: this can throw IOException if a conflicting file exists at the path
            directory.Create();
        }

        var metadataDirectory = directory.GetSubdirectory(this.metadataDirectoryName);
        if (metadataDirectory.Exists)
        {
            throw new InvalidOperationException($"A tandoku {this.packageTypeName} already exists in the specified directory.");
        }
        else
        {
            metadataDirectory.Create();
        }

        var versionFile = metadataDirectory.GetFile(VersionFileName);
        await version.WriteToAsync(versionFile);
    }

    internal async Task<TVersion> GetPackageMetadataAsync(IDirectoryInfo directory)
    {
        var metadataDirectory = directory.GetSubdirectory(this.metadataDirectoryName);

        var versionFile = metadataDirectory.GetFile(VersionFileName);
        var version = versionFile.Exists ?
            await IPackageVersion<TVersion>.ReadFromAsync(versionFile) :
            throw new ArgumentException($"The specified directory is not a valid tandoku {this.packageTypeName}.");

        return version;
    }
}
