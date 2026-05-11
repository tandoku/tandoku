namespace Tandoku.CommandLine.Tests.Common;

using System.CommandLine;
using System.CommandLine.Parsing;

public class ArgumentFactoryTests
{
    [Test]
    public void InputPath_ParsesDirectoryArgument()
    {
        var arg = ArgumentFactory.InputPath();
        var cmd = new RootCommand { arg };

        var result = cmd.Parse("some/path");
        result.Errors.Should().BeEmpty();
        result.GetValue(arg)!.Name.Should().Be("path");
    }

    [Test]
    public void OutputPath_ParsesDirectoryArgument()
    {
        var arg = ArgumentFactory.OutputPath();
        var cmd = new RootCommand { arg };

        var result = cmd.Parse("out");
        result.Errors.Should().BeEmpty();
        result.GetValue(arg)!.Name.Should().Be("out");
    }

    [Test]
    public void InputAndOutputPath_HaveDistinctNames()
    {
        ArgumentFactory.InputPath().Name.Should().Be("input-path");
        ArgumentFactory.OutputPath().Name.Should().Be("output-path");
    }
}
