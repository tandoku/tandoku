namespace Tandoku.Media;

using System.IO.Abstractions;
using Tandoku.Volume;

public static class MediaVolumeExtensions
{
    public static IDirectoryInfo GetImagesDirectory(this VolumeInfo volumeInfo, IFileSystem fileSystem) =>
        fileSystem.GetDirectory(volumeInfo.Path).GetSubdirectory("images");
}
