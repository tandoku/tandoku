namespace Tandoku.CommandLine;

using System.CommandLine;

internal sealed record InputOutputPathArgs(DirectoryInfo InputPath, DirectoryInfo OutputPath);

internal sealed class InputOutputPathArgsBinder : ICommandBinder
{
    internal readonly Argument<DirectoryInfo> InputPathArgument = new("input-path")
    {
        Description = "Path of input content directory",
        Arity = ArgumentArity.ExactlyOne,
    };

    internal readonly Argument<DirectoryInfo> OutputPathArgument = new("output-path")
    {
        Description = "Path of output content directory",
        Arity = ArgumentArity.ExactlyOne,
    };

    public void AddToCommand(Command command)
    {
        command.Arguments.Add(this.InputPathArgument);
        command.Arguments.Add(this.OutputPathArgument);
    }

    internal InputOutputPathArgs Resolve(ParseResult parseResult)
    {
        var inputPath = parseResult.GetValue(this.InputPathArgument)!;
        var outputPath = parseResult.GetValue(this.OutputPathArgument)!;
        return new(inputPath, outputPath);
    }
}
