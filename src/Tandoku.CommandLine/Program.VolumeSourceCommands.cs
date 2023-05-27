namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Volume;

public sealed partial class Program
{
    private Command CreateSourceCommand() =>
        new("source", "Commands for working with tandoku volume sources")
        {
            this.CreateSourceImportCommand(),
        };

    private Command CreateSourceImportCommand()
    {
        var pathsArgument = new Argument<FileSystemInfo[]>("paths", "Paths of files or directories to import as sources") { Arity = ArgumentArity.OneOrMore }
            .LegalFilePathsOnly();
        var volumeBinder = this.CreateVolumeBinder();

        var command = new Command("import", "Imports files from the specified paths as sources for the current or specified volume")
        {
            pathsArgument,
            volumeBinder.VolumeOption,
        };

        command.SetHandler(async (paths, volumeDirectory) =>
        {
            var volumeManager = this.CreateVolumeManager();
            var volumeInfo = await volumeManager.GetInfoAsync(volumeDirectory.FullName);
            var sourceManager = this.CreateSourceManager(volumeInfo);
            var importedFiles = await sourceManager.ImportFilesAsync(paths.Select(p => p.FullName));
            foreach (var importedFile in importedFiles)
                this.console.WriteLine($"Added {importedFile}");
        }, pathsArgument, volumeBinder);

        return command;
    }

    private SourceManager CreateSourceManager(VolumeInfo volumeInfo) => new SourceManager(volumeInfo, this.fileSystem);
}
