namespace Tandoku.Tests.Library;

using System.IO.Abstractions.TestingHelpers;
using Tandoku.Library;

public class LibraryOperationsTests
{
    [Fact]
    public async Task Initialize()
    {
        var mockFileSystem = new MockFileSystem();
        var libraryRootPath = @"c:\tandoku\library";
        var metadataPath = Path.Combine(libraryRootPath, "library.tdkl.yaml");
        var ops = new LibraryOperations(mockFileSystem);

        var info = await ops.InitializeAsync(mockFileSystem.DirectoryInfo.New(libraryRootPath));

        info.Path.Should().Be(libraryRootPath);
        info.MetadataPath.Should().Be(metadataPath);
        mockFileSystem.AllFiles.Count().Should().Be(1);
        mockFileSystem.GetFile(metadataPath).TextContents.Should().Be("language: ja");
    }
}