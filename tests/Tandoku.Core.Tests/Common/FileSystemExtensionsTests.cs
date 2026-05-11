namespace Tandoku.Tests.Common;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

public class FileSystemExtensionsTests
{
    // Note: cast IFileSystem so the GetFile extension method isn't shadowed by
    // MockFileSystem.GetFile(string) which returns MockFileData.
    private readonly MockFileSystem mockFs = new();
    private IFileSystem Fs => this.mockFs;

    [Test]
    public void GetDirectory_GetFile_AndGetCurrentDirectory()
    {
        var dir = this.Fs.GetDirectory("/some/dir");
        dir.FullName.Should().Be(this.Fs.Path.GetFullPath("/some/dir"));

        var file = this.Fs.GetFile("/some/file.txt");
        file.FullName.Should().Be(this.Fs.Path.GetFullPath("/some/file.txt"));

        var current = this.Fs.GetCurrentDirectory();
        current.FullName.Should().Be(this.Fs.Directory.GetCurrentDirectory());
    }

    [Test]
    public void EnumerateFiles_DirectoryExists_ReturnsAllFiles()
    {
        var root = this.Fs.Path.Combine(this.Fs.Directory.GetCurrentDirectory(), "data");
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "a.txt"), new MockFileData("a"));
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "b.txt"), new MockFileData("b"));

        var files = this.Fs.EnumerateFiles(root).Select(f => f.Name).OrderBy(n => n).ToArray();
        files.Should().Equal("a.txt", "b.txt");
    }

    [Test]
    public void EnumerateFiles_DirectoryMissing_TreatsLastSegmentAsPattern()
    {
        var root = this.Fs.Path.Combine(this.Fs.Directory.GetCurrentDirectory(), "data");
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "a.txt"), new MockFileData("a"));
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "b.log"), new MockFileData("b"));

        var pattern = this.Fs.Path.Combine(root, "*.txt");
        var files = this.Fs.EnumerateFiles(pattern).Select(f => f.Name).ToArray();
        files.Should().Equal("a.txt");
    }

    [Test]
    public void EnumerateFiles_NoParent_ReturnsEmpty()
    {
        // Path with no parent directory at all
        this.Fs.EnumerateFiles("nope").Should().BeEmpty();
    }

    [Test]
    public void EnumerateFilesByExtension_FiltersAndPreservesOrder()
    {
        var root = this.Fs.Path.Combine(this.Fs.Directory.GetCurrentDirectory(), "data");
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "a.txt"), new MockFileData("a"));
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "b.log"), new MockFileData("b"));
        this.mockFs.AddFile(this.Fs.Path.Combine(root, "c.txt"), new MockFileData("c"));
        var dir = this.Fs.GetDirectory(root);

        var byExt = dir.EnumerateFilesByExtension(".txt", ".log")
            .Select(f => f.Name)
            .ToArray();

        byExt.Should().BeEquivalentTo(new[] { "a.txt", "c.txt", "b.log" }, opts => opts.WithStrictOrdering());
    }

    [Test]
    public void GetPath_GetSubdirectory_GetFile_OnDirectory()
    {
        var dir = this.Fs.GetDirectory("/root");
        var sub = dir.GetSubdirectory("inner");
        sub.FullName.Should().Be(this.Fs.Path.GetFullPath("/root/inner"));

        var file = dir.GetFile("a.txt");
        file.FullName.Should().Be(this.Fs.Path.GetFullPath("/root/a.txt"));

        dir.GetPath("x").Should().Be(this.Fs.Path.Join(dir.FullName, "x"));
    }

    [Test]
    public void ExtensionEquals_IsCaseInsensitive()
    {
        this.mockFs.AddFile("/a.YAML", new MockFileData(""));
        var file = this.Fs.GetFile("/a.YAML");
        file.ExtensionEquals(".yaml").Should().BeTrue();
        file.ExtensionEquals(".txt").Should().BeFalse();
    }

    [Test]
    [Arguments("notes.txt", null)]
    [Arguments("foo.yaml", null)]
    [Arguments("foo.content.yaml", "foo")]
    [Arguments("FOO.CONTENT.YAML", "FOO")]
    [Arguments("a.b.content.yaml", "a.b")]
    public void GetBaseName_OnPath(string fileName, string? expected)
    {
        this.Fs.Path.GetBaseName(fileName).Should().Be(expected);
    }

    [Test]
    public void GetBaseName_OnFile()
    {
        this.mockFs.AddFile("/foo.content.yaml", new MockFileData(""));
        var file = this.Fs.GetFile("/foo.content.yaml");
        file.GetBaseName().Should().Be("foo");
    }

    [Test]
    public void GetComparer_IsOrdinalIgnoreCase()
    {
        this.Fs.Path.GetComparer().Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public void CleanInvalidFileNameChars_ReplacesAndTrims()
    {
        var invalid = this.Fs.Path.GetInvalidFileNameChars();
        var name = invalid.Length > 0
            ? "a" + invalid[0] + "b" + invalid[0]
            : "ab";
        var cleaned = this.Fs.CleanInvalidFileNameChars(name);
        cleaned.Should().NotContainAny(invalid.Select(c => c.ToString()));
    }

    [Test]
    public void CleanInvalidFileNameChars_TrimsLeadingTrailingWhitespace()
    {
        var invalid = this.Fs.Path.GetInvalidFileNameChars();
        if (invalid.Length == 0)
            return;

        // Trim() removes whitespace, not the replacement character.
        var sep = invalid[0];
        var input = "  foo" + sep + "bar  ";
        var cleaned = this.mockFs.CleanInvalidFileNameChars(input, "_");
        cleaned.Should().Be("foo_bar");
    }
}
