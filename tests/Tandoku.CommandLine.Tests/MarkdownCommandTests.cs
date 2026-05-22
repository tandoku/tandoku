namespace Tandoku.CommandLine.Tests;

using System.IO.Abstractions.TestingHelpers;

public class MarkdownCommandTests : CliTestBase
{
    private const string SampleContent =
        "source:\n" +
        "  note: Opening\n" +
        "chunks:\n" +
        "- text: |-\n" +
        "    むかし[むかし]、ある所にお爺さんとお婆さんが住んでいました。\n" +
        "  references:\n" +
        "    en:\n" +
        "      text: Once upon a time.\n";

    [Test]
    public async Task Export_WritesPerFileMarkdown()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        this.fileSystem.AddFile(inputDir.GetFile("ep01.content.yaml"), new MockFileData(SampleContent));

        await this.RunAndAssertAsync(
            $"markdown export {inputDir.FullName} {outDir.FullName} --ruby Html",
            expectedOutput: $"Wrote {outDir.GetFile("ep01.md").FullName}");

        var md = this.fileSystem.GetFile(outDir.GetFile("ep01.md")).TextContents;
        md.Should().Contain("# Opening");
        md.Should().Contain("<ruby>むかし<rt>むかし</rt></ruby>");
        md.Should().Contain("Once upon a time.");
    }

    [Test]
    public async Task Export_RejectsInvalidRubyOption()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");

        var output = await this.RunAsync(
            $"markdown export {inputDir.FullName} {outDir.FullName} --ruby bogus");

        output.Result.Should().NotBe(0);
        output.Error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Export_UsesCustomTemplate()
    {
        var inputDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("in");
        var outDir = this.fileSystem.GetCurrentDirectory().CreateSubdirectory("out");
        var templateFile = this.fileSystem.GetCurrentDirectory().GetFile("custom.scriban-md");

        this.fileSystem.AddFile(inputDir.GetFile("ep01.content.yaml"), new MockFileData(SampleContent));
        this.fileSystem.AddFile(templateFile, new MockFileData(
            "{{- for chunk in chunks -}}\nCUSTOM: {{ chunk.text }}\n{{ end -}}\n"));

        await this.RunAndAssertAsync(
            $"markdown export {inputDir.FullName} {outDir.FullName} --template {templateFile.FullName}",
            expectedOutput: $"Wrote {outDir.GetFile("ep01.md").FullName}");

        var md = this.fileSystem.GetFile(outDir.GetFile("ep01.md")).TextContents;
        md.Should().Contain("CUSTOM: ");
        md.Should().NotContain("# Opening");
    }
}
