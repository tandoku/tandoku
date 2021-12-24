using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tandoku.CommandLine.Legacy
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length==0)
            {
                Console.WriteLine("Missing path");
                return;
            }

            if (args[0] == "debug")
                System.Diagnostics.Debugger.Launch();

            using (new Utf8EncodingOverride())
            {
                TextExtractionDemo.ExtractText(args.Last());
            }
        }

        // TODO: move to common utils
        private sealed class Utf8EncodingOverride : IDisposable
        {
            private readonly Encoding originalEncoding;

            public Utf8EncodingOverride()
            {
                if (!(Console.OutputEncoding is UTF8Encoding))
                {
                    this.originalEncoding = Console.OutputEncoding;
                    Console.OutputEncoding = new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: false);
                }
            }

            public void Dispose()
            {
                if (this.originalEncoding != null)
                    Console.OutputEncoding = this.originalEncoding;
            }
        }
    }
}
