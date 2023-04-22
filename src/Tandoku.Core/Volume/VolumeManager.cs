namespace Tandoku.Volume;

using System.IO.Abstractions;
using Tandoku.Packaging;

public sealed class VolumeManager
{
    private const string VolumeDefinitionFileName = "volume.yaml";

    private readonly IFileSystem fileSystem;

    public VolumeManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<VolumeInfo> CreateNewAsync(
        string title,
        string path,
        string? moniker = null,
        bool force = false)
    {
        // TODO: check that moniker does not have invalid chars

        var packager = CreatePackager();
        var containerDirectory = this.fileSystem.GetDirectory(path);
        var volumeDirectory = containerDirectory.GetSubdirectory(
            this.GetVolumeDirectoryName(title, moniker));
        var version = VolumeVersion.Latest;
        await packager.InitializePackageAsync(volumeDirectory, version, force);

        // TODO: write definition

        return new VolumeInfo(volumeDirectory.FullName, version);
    }

    public async Task<VolumeInfo> GetInfoAsync(string volumePath)
    {
        var packager = CreatePackager();
        var volumeDirectory = this.fileSystem.GetDirectory(volumePath);
        var version = await packager.GetPackageMetadataAsync(volumeDirectory);

        return new VolumeInfo(volumeDirectory.FullName, version);
    }

    private static Packager<VolumeVersion> CreatePackager() => new("volume");

    private string GetVolumeDirectoryName(string title, string? moniker)
    {
        // TODO: assert that moniker does not have invalid chars

        var cleanTitle = this.fileSystem.CleanInvalidFileNameChars(title);
        return string.IsNullOrEmpty(moniker) ? cleanTitle : $"{moniker}-{cleanTitle}";
    }
}
