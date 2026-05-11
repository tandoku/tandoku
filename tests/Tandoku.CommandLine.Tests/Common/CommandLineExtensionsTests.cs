namespace Tandoku.CommandLine.Tests.Common;

using System.CommandLine;
using System.Text.Json;

public class CommandLineExtensionsTests
{
    [Test]
    [Arguments(null, "<none>")]
    [Arguments("", "")]
    [Arguments("hello", "hello")]
    public void ToOutputString_String_HandlesNullAsPlaceholder(string? input, string expected)
    {
        input.ToOutputString().Should().Be(expected);
    }

    [Test]
    public void ToOutputString_Enumerable_NullOrEmpty_ReturnsPlaceholder()
    {
        ((IEnumerable<string>?)null).ToOutputString().Should().Be("<none>");
        Array.Empty<string>().ToOutputString().Should().Be("<none>");
    }

    [Test]
    public void ToOutputString_Enumerable_JoinsWithCommas()
    {
        new[] { "a", "b", "c" }.ToOutputString().Should().Be("a, b, c");
    }

    [Test]
    public void WriteJsonOutput_WritesIndentedCamelCaseJson()
    {
        using var writer = new StringWriter();
        writer.WriteJsonOutput(new { FooBar = "value", Number = 42 });

        var written = writer.ToString();
        written.Should().Contain("\"fooBar\": \"value\"");
        written.Should().Contain("\"number\": 42");
        written.TrimEnd().Should().EndWith("}");
    }

    [Test]
    public void WriteJsonOutput_DoesNotEscapeJapaneseCharacters()
    {
        using var writer = new StringWriter();
        writer.WriteJsonOutput(new { Text = "日本語" });

        writer.ToString().Should().Contain("日本語");
    }

    [Test]
    public void GetRequiredValues_TwoArguments_ReturnsBoth()
    {
        var arg1 = new Argument<string>("a");
        var arg2 = new Argument<string>("b");
        var cmd = new RootCommand { arg1, arg2 };
        var parse = cmd.Parse("one two");

        var (v1, v2) = parse.GetRequiredValues(arg1, arg2);
        v1.Should().Be("one");
        v2.Should().Be("two");
    }

    [Test]
    public void GetValues_ThreeOptions_ReturnsTuple()
    {
        var o1 = new Option<string?>("--a");
        var o2 = new Option<int>("--b");
        var o3 = new Option<bool>("--c");
        var cmd = new RootCommand { o1, o2, o3 };
        var parse = cmd.Parse("--a foo --b 5 --c");

        var (a, b, c) = parse.GetValues(o1, o2, o3);
        a.Should().Be("foo");
        b.Should().Be(5);
        c.Should().BeTrue();
    }
}
