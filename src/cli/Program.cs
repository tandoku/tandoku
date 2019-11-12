using System;

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace BlueMarsh.Tandoku.CommandLine
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args?.Length > 1 && args[0] == "demo")
                Demos.Run(args.AsSpan(1));
            else
                TokenizerDemo.Dump();
        }
    }
}
