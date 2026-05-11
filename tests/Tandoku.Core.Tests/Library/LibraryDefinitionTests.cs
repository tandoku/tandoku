namespace Tandoku.Tests.Library;

using Tandoku.Library;
using Tandoku.Tests.Serialization;

public class LibraryDefinitionTests
{
    [Test]
    public async Task RoundTripsViaYaml()
    {
        var def = new LibraryDefinition { Language = "ja" };

        using var writer = new StringWriter();
        await YamlTestHelpers.WriteAsync(def, writer);
        var yaml = writer.ToString();
        yaml.TrimEnd().Should().Be("language: ja");

        var parsed = await YamlTestHelpers.ReadAsync<LibraryDefinition>(new StringReader(yaml));
        parsed.Should().Be(def);
    }
}

public class LibraryVersionTests
{
    [Test]
    public void Latest_HasExpectedVersion()
    {
        LibraryVersion.Latest.Version.Should().Be(new Version(0, 1, 0));
    }

    [Test]
    public void TryGet_ReturnsLatest_ForMatchingVersion()
    {
        LibraryVersion.TryGet(new Version(0, 1, 0), out var v).Should().BeTrue();
        v.Should().BeSameAs(LibraryVersion.Latest);
    }

    [Test]
    public void TryGet_ReturnsFalse_ForUnknownVersion()
    {
        LibraryVersion.TryGet(new Version(99, 0, 0), out var v).Should().BeFalse();
        v.Should().BeNull();
    }

    [Test]
    public void ToString_ReturnsVersionString()
    {
        LibraryVersion.Latest.ToString().Should().Be("0.1.0");
    }
}
