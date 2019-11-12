using System;

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace BlueMarsh.Tandoku.CommandLine
{
    internal static class Demos
    {
        internal static void Run(Span<string> args)
        {
            if (args.Length > 1 && args[0] == "extract")
            {
                TextExtractionDemo.ExtractText(args[1]);
                return;
            }
            else if (args.Length == 1 && args[0] == "dict")
            {
                DictionaryLookupDemo.Run();
                return;
            }

            if (args.Length == 1 && args[0] == "debug")
            {
                System.Diagnostics.Debugger.Launch();
            }

            TokenizerDemo.Dump();
        }
    }
}
