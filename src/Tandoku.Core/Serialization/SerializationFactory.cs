namespace Tandoku.Serialization;

using System.Text.Encodings.Web;
using System.Text.Json;
using Tandoku.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class SerializationFactory
{
    internal static JsonSerializerOptions JsonOptions { get; } = CreateJsonSerializerOptions();

    internal static ISerializer CreateYamlSerializer() =>
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithQuotingNecessaryStrings()
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull |
                DefaultValuesHandling.OmitDefaults |
                DefaultValuesHandling.OmitEmptyCollections)
            .DisableAliases()
            .WithEventEmitter(next => new FlowStyleEventEmitter(next))
            .Build();

    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // avoid escaping Japanese characters
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
