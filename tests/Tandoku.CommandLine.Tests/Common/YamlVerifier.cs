namespace Tandoku.CommandLine.Tests;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class YamlVerifier
{
    public static SettingsTask VerifyYaml(object target)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var output = serializer.Serialize(target);
        return Verify(output, "yaml");
    }
}
