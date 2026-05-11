namespace Tandoku.CommandLine.Tests.Common;

using System.CommandLine;

public class EnumOptionTests
{
    public enum Sample
    {
        Simple,
        TwoWords,
        ABCThing,
    }

    [Test]
    public void GetAcceptedValues_ConvertsPascalCaseToKebabCase()
    {
        EnumOption.GetAcceptedValues<Sample>()
            .Should().BeEquivalentTo("simple", "two-words", "a-b-c-thing");
    }

    [Test]
    [Arguments("simple", Sample.Simple)]
    [Arguments("two-words", Sample.TwoWords)]
    [Arguments("a-b-c-thing", Sample.ABCThing)]
    public void EnumOption_ParsesKebabCaseValues(string input, Sample expected)
    {
        var option = new EnumOption<Sample>("--mode");
        var cmd = new RootCommand { option };

        var result = cmd.Parse($"--mode {input}");
        result.Errors.Should().BeEmpty();
        result.GetValue(option).Should().Be(expected);
    }

    [Test]
    public void EnumOption_RejectsInvalidValue()
    {
        var option = new EnumOption<Sample>("--mode");
        var cmd = new RootCommand { option };

        var result = cmd.Parse("--mode bogus");
        result.Errors.Should().NotBeEmpty();
    }

    [Test]
    public void NullableEnumOption_DefaultIsNull()
    {
        var option = new NullableEnumOption<Sample>("--mode");
        var cmd = new RootCommand { option };

        var result = cmd.Parse(string.Empty);
        result.GetValue(option).Should().BeNull();
    }

    [Test]
    public void NullableEnumOption_ParsesKebabCaseValue()
    {
        var option = new NullableEnumOption<Sample>("--mode");
        var cmd = new RootCommand { option };

        var result = cmd.Parse("--mode two-words");
        result.GetValue(option).Should().Be(Sample.TwoWords);
    }
}
