#:package Tomlyn@*
#:property JsonSerializerIsReflectionEnabledByDefault=true

// NOTE: TOML requires strings to be quoted, and PowerShell eats quotes by default, must invoke as follows:
//  dotnet run toml-config.cs -- core.quirks='"nook"'
// the single quotes are consumed by PowerShell and the actual argument is:
//  core.quirks="nook"
// Other shells may behave differently. Legacy cmd shell in Windows eats " but not ' so core.quirks='nook' works

using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

if (args.Length > 0)
{
    foreach (var arg in args)
    {
        Console.WriteLine($"Input: {arg}");

        var tomlObj = TomlSerializer.Deserialize<TomlTable>(arg);

        var toml = TomlSerializer.Serialize(tomlObj);
        Console.WriteLine("TOML:");
        Console.WriteLine(toml);

        var json = JsonSerializer.Serialize(tomlObj, jsonOptions);
        Console.WriteLine("JSON:");
        Console.WriteLine(json);

        Console.WriteLine();
    }
}
else
{
    var toml = @"global = ""this is a string""
    # This is a comment of a table
    [my_table]
    key = 1 # Comment a key
    value = true
    list = [4, 5, 6]
    ";

    var model = TomlSerializer.Deserialize<TomlTable>(toml)!;
    var global = (string)model["global"]!;

    Console.WriteLine(global);
    Console.WriteLine(TomlSerializer.Serialize(model));

    // Convert the TomlTable to JSON using System.Text.Json
    var json = JsonSerializer.Serialize(model, jsonOptions);

    Console.WriteLine("JSON output:");
    Console.WriteLine(json);
}