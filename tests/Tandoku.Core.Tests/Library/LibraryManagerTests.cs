namespace Tandoku.Tests.Library;

using Tandoku.Library;
using Spectre.IO;
using Spectre.IO.Testing;

public class LibraryManagerTests
{
    [Fact]
    public async Task Initialize()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        var definitionPath = libraryRootPath.CombineWithFilePath("library.tdkl.yaml");

        var info = await libraryManager.InitializeAsync(libraryRootPath.FullPath);

        info.Path.Should().Be(libraryRootPath.FullPath);
        info.DefinitionPath.Should().Be(definitionPath.FullPath);
        fileSystem.ToString().Should().Be(@"C:
    Working
        tandoku-library
            library.tdkl.yaml");
        fileSystem.GetFakeFile(definitionPath).GetTextContent().TrimEnd().Should().Be(
@"language: ja
referenceLanguage: en");
    }

    [Fact]
    public async Task InitializeWithConflictingFile()
    {
        // TODO: Spectre.IO doesn't check for files at all when creating directories,
        // cannot pass this test without explicitly checking for existence of file in InitializeAsync

        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.CreateFile(libraryRootPath.FullPath).SetTextContent("existing");

        await libraryManager.Invoking(m => m.InitializeAsync(libraryRootPath.FullPath))
            .Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task InitializeWithNonEmptyDirectory()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.CreateFile(libraryRootPath.CombineWithFilePath("existing.txt"));

        await libraryManager.Invoking(m => m.InitializeAsync(libraryRootPath.FullPath, force: false))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeWithNonEmptyDirectoryForce()
    {
        var (libraryManager, fileSystem, libraryRootPath) = Setup();
        fileSystem.CreateFile(libraryRootPath.CombineWithFilePath("existing.txt"));

        var info = await libraryManager.InitializeAsync(libraryRootPath.FullPath, force: true);

        info.Path.Should().Be(libraryRootPath.FullPath);
        fileSystem.ToString().Should().Be(@"C:
    Working
        tandoku-library
            existing.txt
            library.tdkl.yaml");
        fileSystem.GetFakeFile(info.DefinitionPath).GetTextContent().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetInfo()
    {
        var (libraryManager, _, libraryRootPath) = Setup();
        var originalInfo = await libraryManager.InitializeAsync(libraryRootPath.FullPath);

        var info = await libraryManager.GetInfoAsync(originalInfo.DefinitionPath);

        info.Should().BeEquivalentTo(originalInfo);
    }

    // TODO: add tests for ResolveLibraryDefinitionPath

    private static (LibraryManager, FakeFileSystem, DirectoryPath libraryRootPath) Setup()
    {
        var environment = new FakeEnvironment(PlatformFamily.Windows); // TODO: run separately for Linux
        var fileSystem = new FakeFileSystem(environment);
        var libraryManager = new LibraryManager(fileSystem);
        var libraryRootPath = environment.WorkingDirectory.Combine("tandoku-library");

        return (libraryManager, fileSystem, libraryRootPath);
    }

}