using System;
using System.Collections.Generic;
using System.Text;
using TikaOnDotNet.TextExtraction;

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace BlueMarsh.Tandoku.CommandLine
{
    class TextExtractionDemo
    {
        internal static void ExtractText(string path)
        {
            var extractor = new TextExtractor();
            var result = extractor.Extract(path);

            Console.WriteLine($"ContentType: {result.ContentType}");
            Console.WriteLine("Metadata:");
            foreach (var pair in result.Metadata)
                Console.WriteLine($"  {pair.Key}: {pair.Value}");
            Console.WriteLine("Text:");
            Console.WriteLine();
            Console.WriteLine(result.Text);
        }
    }
}
