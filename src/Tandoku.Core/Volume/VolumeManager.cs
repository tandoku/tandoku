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

    public async Task<VolumeInfo> InitializeAsync(string path, bool force = false)
    {
        var directory = this.fileSystem.GetDirectory(path);
        var version = VolumeVersion.Latest;
        var packager = CreatePackager();
        await packager.InitializePackageAsync(directory, version, force);

        // TODO: write definition

        return new VolumeInfo(directory.FullName, version);
    }

    //public async Task<VolumeInfo> GetInfoAsync(string volumePath)
    //{
    //}

    private static Packager<VolumeVersion> CreatePackager() => new("volume");
}
