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
        this.RunAndVerifyAsync(new MarkdownExportSettings { RubyBehavior = ruby });

    [Test]
    public Task Export_KeepTogether() =>
        this.RunAndVerifyAsync(new MarkdownExportSettings { KeepTogether = true });

    [Test]
    public Task Export_NoHeadings() =>
        this.RunAndVerifyAsync(new MarkdownExportSettings
        {
            NoHeadings = true,
            RubyBehavior = MarkdownRubyBehavior.Remove,
        });

    [Test]
    public Task Export_Footnotes() =>
        this.RunAndVerifyAsync(new MarkdownExportSettings
        {
            RubyBehavior = MarkdownRubyBehavior.Html,
            ReferenceBehavior = MarkdownReferenceBehavior.Footnotes,
        });

    [Test]
    public Task Export_KyBook3() =>
        this.RunAndVerifyAsync(new MarkdownExportSettings
        {
            Quirks = MarkdownQuirks.KyBook3,
            RubyBehavior = MarkdownRubyBehavior.Html,
            ReferenceBehavior = MarkdownReferenceBehavior.Footnotes,
        });

    [Test]
    public Task Export_BlurHtmlReferences() =>
        this.RunAndVerifyAsync(new MarkdownExportSettings
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
        var exporter = new MarkdownExporter(new MarkdownExportSettings
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

    private async Task RunAndVerifyAsync(MarkdownExportSettings options)
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
