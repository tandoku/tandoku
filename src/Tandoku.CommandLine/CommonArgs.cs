namespace Tandoku.CommandLine;

using System.CommandLine;

// TODO - just use a tuple instead of defining a record type for this
internal sealed record InputOutputPathArgs(DirectoryInfo InputPath, DirectoryInfo OutputPath);

internal sealed class InputOutputPathArgsBinder : ICommandBinder<InputOutputPathArgs>
{
    private readonly Argument<DirectoryInfo> inputPathArgument = new Argument<DirectoryInfo>("input-path")
    {
        Description = "Path of input content directory",
        Arity = ArgumentArity.ExactlyOne,
    }.AcceptLegalFilePathsOnly();

    private readonly Argument<DirectoryInfo> outputPathArgument = new Argument<DirectoryInfo>("output-path")
    {
        Description = "Path of output content directory",
        Arity = ArgumentArity.ExactlyOne,
    }.AcceptLegalFilePathsOnly();

    public void AddToCommand(Command command)
    {
        command.Arguments.Add(this.inputPathArgument);
        command.Arguments.Add(this.outputPathArgument);
    }

    public InputOutputPathArgs Resolve(ParseResult parseResult)
    {
        var inputPath = parseResult.GetValue(this.inputPathArgument)!;
        var outputPath = parseResult.GetValue(this.outputPathArgument)!;
        return new(inputPath, outputPath);
    }

    public static implicit operator Parameter<InputOutputPathArgs>(InputOutputPathArgsBinder binder) => new(binder);
}
