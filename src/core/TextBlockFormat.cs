﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlueMarsh.Tandoku
{
    public enum TextBlockFormat
    {
        Jsonl,
        Yaml,
    }

    public static class TextBlockFormatExtensions
    {
        private const string JsonlExtension = ".jsonl";
        private const string YamlExtension = ".yaml";

        public static TextBlockFormat? FromFileExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                JsonlExtension => TextBlockFormat.Jsonl,
                YamlExtension => TextBlockFormat.Yaml,
                _ => throw new ArgumentOutOfRangeException(nameof(extension)),
            };
        }

        public static TextBlockFormat? FromFilePath(string path)
        {
            return FromFileExtension(Path.GetExtension(path));
        }

        public static string ToFileExtension(this TextBlockFormat format)
        {
            return format switch
            {
                TextBlockFormat.Jsonl => JsonlExtension,
                TextBlockFormat.Yaml => YamlExtension,
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
        }
    }
}
