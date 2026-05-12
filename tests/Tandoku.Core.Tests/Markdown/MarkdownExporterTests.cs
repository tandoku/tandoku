namespace Tandoku.Tests.Markdown;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Tandoku.Markdown;

public class MarkdownExporterTests
{
    [Test]
    [Arguments(MarkdownRubyBehavior.None)]
    [Arguments(MarkdownRubyBehavior.Html)]
    [Arguments(MarkdownRubyBehavior.Remove)]
    public Task Export_RubyVariants(MarkdownRubyBehavior ruby) =>
        this.RunAndVerifyAsync(new MarkdownExportOptions { RubyBehavior = ruby });

    [Test]
    public Task Export_KeepTogether() =>
        this.RunAndVerifyAsync(new MarkdownExportOptions { KeepTogether = true });

    [Test]
    public Task Export_NoHeadings() =>
        this.RunAndVerifyAsync(new MarkdownExportOptions
        {
            NoHeadings = true,
            RubyBehavior = MarkdownRubyBehavior.Remove,
        });

    [Test]
    public Task Export_Footnotes() =>
        this.RunAndVerifyAsync(new MarkdownExportOptions
        {
            RubyBehavior = MarkdownRubyBehavior.Html,
            ReferenceBehavior = MarkdownReferenceBehavior.Footnotes,
        });

    [Test]
    public Task Export_KyBook3() =>
        this.RunAndVerifyAsync(new MarkdownExportOptions
        {
            Quirks = MarkdownQuirks.KyBook3,
            RubyBehavior = MarkdownRubyBehavior.Html,
            ReferenceBehavior = MarkdownReferenceBehavior.Footnotes,
        });

    [Test]
    public Task Export_BlurHtmlReferences() =>
        this.RunAndVerifyAsync(new MarkdownExportOptions
        {
            RubyBehavior = MarkdownRubyBehavior.BlurHtml,
            ReferenceBehavior = MarkdownReferenceBehavior.BlurHtml,
        });

    [Test]
    public async Task Export_Combined_WritesSingleFile()
    {
        var fs = new MockFileSystem();
        var inputDir = fs.GetCurrentDirectory().CreateSubdirectory("in");
        WriteSampleResource(fs, inputDir.GetFile("ep01.content.yaml"), "ep01.content.yaml");
        WriteSampleResource(fs, inputDir.GetFile("ep02.content.yaml"), "ep01.content.yaml");

        var outDir = fs.GetCurrentDirectory().CreateSubdirectory("out");
        var exporter = new MarkdownExporter(new MarkdownExportOptions
        {
            Combine = true,
            NoHeadings = true,
        }, fs);
        var written = await exporter.ExportAsync(inputDir.FullName, outDir.FullName);

        written.Should().HaveCount(1);
        var combined = fs.File.ReadAllText(written[0]);
        combined.Should().Contain("# ep01");
        combined.Should().Contain("# ep02");
    }

    private async Task RunAndVerifyAsync(MarkdownExportOptions options)
    {
        var fs = new MockFileSystem();
        var inputDir = fs.GetCurrentDirectory().CreateSubdirectory("in");
        WriteSampleResource(fs, inputDir.GetFile("ep01.content.yaml"), "ep01.content.yaml");
        var outDir = fs.GetCurrentDirectory().CreateSubdirectory("out");

        var exporter = new MarkdownExporter(options, fs);
        var written = await exporter.ExportAsync(inputDir.FullName, outDir.FullName);

        written.Should().HaveCount(1);
        var markdown = fs.File.ReadAllText(written[0]);

        await Verify(markdown, "md");
    }

    private static void WriteSampleResource(MockFileSystem fs, IFileInfo file, string resourceName)
    {
        using var stream = typeof(MarkdownExporterTests).GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        fs.AddFile(file, new MockFileData(reader.ReadToEnd()));
    }
}
