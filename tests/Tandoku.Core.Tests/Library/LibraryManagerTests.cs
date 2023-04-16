namespace Tandoku.Tests.Library;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Library;

public class LibraryManagerTests
{
    [Fact]
    public async Task Initialize()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        var definitionPath = Path.Join(libraryRootPath, "library.tdkl.yaml");

        var info = await libraryManager.InitializeAsync(libraryRootPath);

        info.Path.Should().Be(libraryRootPath);
        info.DefinitionPath.Should().Be(definitionPath);
        fileSystem.AllFiles.Count().Should().Be(1);
        fileSystem.GetFile(definitionPath).TextContents.TrimEnd().Should().Be(
@"language: ja
referenceLanguage: en");
    }

    [Fact]
    public async Task InitializeWithConflictingFile()
    {
        // Note: with real System.IO, the ReadOnly attribute on the file isn't necessary
        // and this would throw an IOException rather than UnauthorizedAccessException.
        // Filed https://github.com/TestableIO/System.IO.Abstractions/issues/968

        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.AddEmptyFile(libraryRootPath);
        fileSystem.File.SetAttributes(libraryRootPath, FileAttributes.ReadOnly);

        await libraryManager.Invoking(m => m.InitializeAsync(libraryRootPath))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task InitializeWithNonEmptyDirectory()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.AddEmptyFile(fileSystem.Path.Join(libraryRootPath, "existing.txt"));

        await libraryManager.Invoking(m => m.InitializeAsync(libraryRootPath, force: false))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeWithNonEmptyDirectoryForce()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.AddEmptyFile(fileSystem.Path.Join(libraryRootPath, "existing.txt"));

        var info = await libraryManager.InitializeAsync(libraryRootPath, force: true);

        info.Path.Should().Be(libraryRootPath);
        fileSystem.AllFiles.Count().Should().Be(2);
        fileSystem.GetFile(info.DefinitionPath).TextContents.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetInfo()
    {
        var (libraryManager, _, libraryRootPath) = Setup();
        var originalInfo = await libraryManager.InitializeAsync(libraryRootPath);

        var info = await libraryManager.GetInfoAsync(originalInfo.DefinitionPath);

        info.Should().BeEquivalentTo(originalInfo);
    }

    private static (LibraryManager, MockFileSystem, string libraryRootPath) Setup()
    {
        var fileSystem = new MockFileSystem();
        var libraryManager = new LibraryManager(fileSystem);
        var libraryRootPath = fileSystem.Path.Join(
            fileSystem.Directory.GetCurrentDirectory(),
            "tandoku-library");

        return (libraryManager, fileSystem, libraryRootPath);
    }

}