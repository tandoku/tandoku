namespace Tandoku.Subtitles;

using System.IO.Abstractions;
using Tandoku.Subtitles.Ttml;

/// <summary>
/// Converter for TTML subtitles to WebVTT format. Preserves ruby annotations.
/// </summary>
public sealed class TtmlToWebVttConverter
{
    private readonly IFileSystem fileSystem;
    private readonly string inputPath;
    private readonly string outputPath;

    public TtmlToWebVttConverter(string inputPath, string outputPath, IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
        this.inputPath = inputPath;
        this.outputPath = outputPath;
    }

    public async Task ConvertAsync()
    {
        var inputDir = this.fileSystem.GetDirectory(this.inputPath);
        var outputDir = this.fileSystem.GetDirectory(this.outputPath);
        outputDir.Create();
        foreach (var inputFile in inputDir.EnumerateTtmlSubtitleFiles())
        {
            // Note - strip both subtitle extension and language code from filename (i.e. abc.ja.srt -> abc.content.yaml)
            var baseName = this.fileSystem.Path.GetFileNameWithoutExtension(inputFile.Name);
            var targetName = this.fileSystem.Path.ChangeExtension(baseName, SubtitleExtensions.WebVtt);
            var outputFile = outputDir.GetFile(targetName);
            var ttmlDocument = await TtmlSerializer.DeserializeAsync(inputFile.OpenRead());
            var webVttContent = ConvertToWebVtt(ttmlDocument);
            // TODO await YamlSerializer.WriteStreamAsync(outputFile, GenerateContentBlocksAsync(inputFile));
        }
    }

    private static object ConvertToWebVtt(TtmlDocument ttmlDocument)
    {
        // TODO - write visitor for TtmlDocument, write to WebVTT objects
        return new object();
    }

    private sealed class ConvertVisitor : TtmlDocumentVisitor
    {
    }
}
