namespace Tandoku.CommandLine.Tests;

using System.Runtime.CompilerServices;

internal static class ModuleInitialization
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VerifyDiffPlex.Initialize();

        Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) => new(
            directory: Path.Combine(projectDirectory, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name));
    }
}
