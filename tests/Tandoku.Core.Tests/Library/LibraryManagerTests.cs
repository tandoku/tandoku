namespace Tandoku.Tests.Library;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Library;

public class LibraryManagerTests
{
    [Fact]
    public async Task Initialize()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        var metadataPath = Path.Join(libraryRootPath, "library.tdkl.yaml");

        var info = await libraryManager.InitializeAsync(libraryRootPath);

        info.Path.Should().Be(libraryRootPath);
        info.MetadataPath.Should().Be(metadataPath);
        fileSystem.AllFiles.Count().Should().Be(1);
        fileSystem.GetFile(metadataPath).TextContents.Should().Be("language: ja");
    }

    [Fact]
    public async Task InitializeWithForceFailure()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.AddEmptyFile(fileSystem.Path.Join(libraryRootPath, "existing.txt"));

        await libraryManager.Invoking(m => m.InitializeAsync(libraryRootPath, force: false))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeWithForceSuccess()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.AddEmptyFile(fileSystem.Path.Join(libraryRootPath, "existing.txt"));

        var info = await libraryManager.InitializeAsync(libraryRootPath, force: true);

        info.Path.Should().Be(libraryRootPath);
        fileSystem.AllFiles.Count().Should().Be(2);
        fileSystem.GetFile(info.MetadataPath).TextContents.Should().NotBeNullOrEmpty();
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