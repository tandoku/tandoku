namespace Tandoku.Tests.Volume;

using System.Collections.Immutable;
using Tandoku.Tests.Serialization;
using Tandoku.Volume;

public class VolumeDefinitionTests
{
    [Test]
    public async Task RoundTripsAllProperties()
    {
        var def = new VolumeDefinition
        {
            Title = "My Volume",
            Language = "ja",
            Tags = ImmutableSortedSet.Create("anime", "drama"),
            LinkedVolumes = ImmutableSortedDictionary<string, LinkedVolume>.Empty
                .Add("subs", new LinkedVolume { Path = "../subs", Moniker = "linked" }),
            Workflow = "wf",
        };

        using var writer = new StringWriter();
        await YamlTestHelpers.WriteAsync(def, writer);
        var yaml = writer.ToString();
        yaml.Should().Contain("title: My Volume");
        yaml.Should().Contain("language: ja");
        // Tags use flow style (IImmutableSet -> FlowStyleEventEmitter)
        yaml.Should().Contain("tags: [anime, drama]");

        var parsed = await YamlTestHelpers.ReadAsync<VolumeDefinition>(new StringReader(yaml));
        parsed.Should().BeEquivalentTo(def);
    }

    [Test]
    public async Task DefaultsAreOmittedFromYaml()
    {
        var def = new VolumeDefinition { Language = "ja" };

        using var writer = new StringWriter();
        await YamlTestHelpers.WriteAsync(def, writer);
        var yaml = writer.ToString();
        yaml.TrimEnd().Should().Be("language: ja");
    }
}

public class VolumeVersionTests
{
    [Test]
    public void Latest_HasExpectedVersion() =>
        VolumeVersion.Latest.Version.Should().Be(new Version(0, 1, 0));

    [Test]
    public void TryGet_ReturnsLatest_ForMatchingVersion()
    {
        VolumeVersion.TryGet(new Version(0, 1, 0), out var v).Should().BeTrue();
        v.Should().BeSameAs(VolumeVersion.Latest);
    }

    [Test]
    public void TryGet_ReturnsFalse_ForUnknownVersion()
    {
        VolumeVersion.TryGet(new Version(2, 0, 0), out var v).Should().BeFalse();
        v.Should().BeNull();
    }

    [Test]
    public void ToString_ReturnsVersionString() =>
        VolumeVersion.Latest.ToString().Should().Be("0.1.0");
}
