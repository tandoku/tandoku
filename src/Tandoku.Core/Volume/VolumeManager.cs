namespace Tandoku.Volume;

using System.IO.Abstractions;
using Tandoku.Library;
using Tandoku.Packaging;

public sealed class VolumeManager
{
    private const string VolumeDefinitionFileName = "volume.yaml";
    private const string VolumeDefinitionPartName = "definition";

    private readonly IFileSystem fileSystem;

    public VolumeManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<VolumeInfo> InitializeAsync(string path, bool force = false)
    {
        var packager = CreatePackager();
        var volumeDirectory = this.fileSystem.GetDirectory(path);
        var version = VolumeVersion.Latest;
        await packager.InitializePackageAsync(volumeDirectory, version, force);

        var definition = new VolumeDefinition
        {
            Language = LanguageConstants.DefaultLanguage,
            //ReferenceLanguage = LanguageConstants.DefaultReferenceLanguage,
        };
        var definitionFile = await packager.WritePackagePart(volumeDirectory, VolumeDefinitionFileName, definition);

        return new VolumeInfo(
            volumeDirectory.FullName,
            volumeDirectory.Name,
            version,
            definitionFile.FullName,
            definition);
    }

    public async Task<VolumeInfo> CreateNewAsync(
        string path,
        string title,
        string? moniker = null,
        IEnumerable<string>? tags = null,
        bool force = false)
    {
        // TODO: check that moniker does not have invalid chars

        var containerDirectory = this.fileSystem.GetDirectory(path);
        var volumeDirectory = containerDirectory.GetSubdirectory(
            this.GetVolumeDirectoryName(title, moniker));
        var info = await this.InitializeAsync(volumeDirectory.FullName, force);
        var definition = info.Definition with
        {
            Title = title,
            Moniker = moniker,
            Tags = info.Definition.Tags.Union(tags ?? []),
        };
        await this.SetDefinitionAsync(volumeDirectory.FullName, definition);

        return info with { Definition = definition };
    }

    public async Task<VolumeInfo> GetInfoAsync(string volumePath)
    {
        return (await this.GetInfoAsyncCore(volumePath)).VolumeInfo;
    }

    private async Task<(VolumeInfo VolumeInfo, IDirectoryInfo VolumeDirectory)> GetInfoAsyncCore(string volumePath)
    {
        var packager = CreatePackager();
        var volumeDirectory = this.fileSystem.GetDirectory(volumePath);
        var version = await packager.GetPackageMetadataAsync(volumeDirectory);

        var (definitionFile, definition) = await packager.ReadPackagePart<VolumeDefinition>(
            volumeDirectory,
            VolumeDefinitionFileName,
            VolumeDefinitionPartName);

        return (new VolumeInfo(volumeDirectory.FullName, volumeDirectory.Name, version, definitionFile.FullName, definition), volumeDirectory);
    }

    public async Task SetDefinitionAsync(string volumePath, VolumeDefinition definition)
    {
        var packager = CreatePackager();
        var volumeDirectory = this.fileSystem.GetDirectory(volumePath);

        await packager.WritePackagePart(volumeDirectory, VolumeDefinitionFileName, definition);
    }

    public async Task<RenameResult> RenameVolumeDirectory(string volumePath)
    {
        var (info, volumeDirectory) = await this.GetInfoAsyncCore(volumePath);

        var newName = this.GetVolumeDirectoryName(info.Definition.Title, info.Definition.Moniker);

        if (newName.Equals(volumeDirectory.Name))
            return new RenameResult(volumeDirectory.Name, volumeDirectory.Name);

        var oldPath = volumeDirectory.FullName; // capture this since it will change after MoveTo()
        var newPath = volumeDirectory.Parent?.GetPath(newName) ??
            throw new InvalidOperationException("Cannot rename volume in a root directory.");

        volumeDirectory.MoveTo(newPath);

        return new RenameResult(oldPath, newPath);
    }

    public IEnumerable<string> GetVolumeDirectories(string path, ExpandedScope expandScope = ExpandedScope.None)
    {
        path = expandScope switch
        {
            ExpandedScope.ParentVolume =>
                this.ResolveVolumeDirectoryPath(path, checkAncestors: true) ?? path,

            ExpandedScope.ParentLibrary =>
                this.CreateLibraryManager().ResolveLibraryDirectoryPath(path, checkAncestors: true) ??
                    throw new ArgumentException("The specified path does not contain a tandoku library."),

            _ => path,
        };

        var baseDirectory = this.fileSystem.GetDirectory(path);
        return CreatePackager().GetPackageDirectories(baseDirectory).Select(d => d.FullName);
    }

    public string? ResolveVolumeDirectoryPath(string path, bool checkAncestors = false)
    {
        var directory = this.fileSystem.GetDirectory(path);
        return CreatePackager().ResolvePackageDirectoryPath(directory, checkAncestors);
    }

    private LibraryManager CreateLibraryManager() => new(this.fileSystem);

    private static Packager<VolumeVersion> CreatePackager() => new("volume");

    private string GetVolumeDirectoryName(string title, string? moniker)
    {
        // TODO: assert that moniker does not have invalid chars

        var cleanTitle = this.fileSystem.CleanInvalidFileNameChars(title);
        return string.IsNullOrEmpty(moniker) ? cleanTitle : $"{moniker}-{cleanTitle}";
    }
}
