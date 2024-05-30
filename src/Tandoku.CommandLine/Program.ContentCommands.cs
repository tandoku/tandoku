namespace Tandoku.CommandLine;

using System.CommandLine;
using Tandoku.Content;

public sealed partial class Program
{
    private Command CreateContentCommand() =>
        new("content", "Commands for working with tandoku content streams")
        {
            this.CreateContentIndexCommand(),
        };

    private Command CreateContentIndexCommand()
    {
        var pathArgument = new Argument<DirectoryInfo>("path", "Path of content directory to index") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();
        var indexPathOption = new Option<DirectoryInfo>("--index-path", "Path of the index to build") { Arity = ArgumentArity.ExactlyOne }
            .LegalFilePathsOnly();

        var command = new Command("index", "Indexes the specified content")
        {
            pathArgument,
            indexPathOption,
        };

        command.SetHandler(async (path, indexPath) =>
        {
            var indexBuilder = new ContentIndexBuilder(this.fileSystem);
            await indexBuilder.BuildAsync(path.FullName, indexPath.FullName);
            this.console.WriteLine($"Wrote index to {indexPath}");
        }, pathArgument, indexPathOption);

        return command;
    }
}
