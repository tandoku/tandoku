namespace BlueMarsh.Tandoku.CommandLine;

using System.CommandLine;
using System.CommandLine.Invocation;

public static class CommandLineExtensions
{
    public static Command WithHandler(this Command command, ICommandHandler handler)
    {
        command.Handler = handler;
        return command;
    }
}
