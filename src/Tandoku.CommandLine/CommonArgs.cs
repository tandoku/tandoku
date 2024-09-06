namespace Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Binding;

internal sealed record InputOutputPathArgs(DirectoryInfo InputPath, DirectoryInfo OutputPath);

internal sealed class InputOutputPathArgsBinder : BinderBase<InputOutputPathArgs>, ICommandBinder
{
    private readonly Argument<DirectoryInfo> inputPathArgument = new Argument<DirectoryInfo>(
        "input-path",
        "Path of input content directory") { Arity = ArgumentArity.ExactlyOne }
        .LegalFilePathsOnly();
    private readonly Argument<DirectoryInfo> outputPathArgument = new Argument<DirectoryInfo>(
        "output-path",
        "Path of output content directory") { Arity = ArgumentArity.ExactlyOne }
        .LegalFilePathsOnly();

    public void AddToCommand(Command command)
    {
        command.Add(this.inputPathArgument);
        command.Add(this.outputPathArgument);
    }

    protected override InputOutputPathArgs GetBoundValue(BindingContext bindingContext)
    {
        var inputPath = bindingContext.ParseResult.GetValueForArgument(this.inputPathArgument);
        var outputPath = bindingContext.ParseResult.GetValueForArgument(this.outputPathArgument);
        return new(inputPath, outputPath);
    }
}
