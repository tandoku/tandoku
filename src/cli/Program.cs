using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BlueMarsh.Tandoku.CommandLine
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                Demos.CreateCommand(),
            };

            rootCommand.Invoke(args);
        }
    }
}
