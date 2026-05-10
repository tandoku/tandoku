namespace Tandoku.CommandLine;

using System.CommandLine;

internal static class ArgumentFactory
{
    internal static Argument<DirectoryInfo> InputPath() => new Argument<DirectoryInfo>("input-path")
    {
        Description = "Path of input content directory",
        Arity = ArgumentArity.ExactlyOne,
    }.AcceptLegalFilePathsOnly();

    internal static Argument<DirectoryInfo> OutputPath() => new Argument<DirectoryInfo>("output-path")
    {
        Description = "Path of output content directory",
        Arity = ArgumentArity.ExactlyOne,
    }.AcceptLegalFilePathsOnly();
}
