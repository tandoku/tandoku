using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tandoku;

public sealed class TextProcessor
{
    public long ProcessedBlocksCount { get; private set; }

    public void Tokenize(string path)
    {
        var serializer = new TextBlockSerializer();
        string tempPath = Path.GetTempFileName();
        var format = TextBlockFormatExtensions.FromFilePath(path);
        serializer.Serialize(tempPath, Tokenize(serializer.Deserialize(path)), format);
        File.Delete(path);
        File.Move(tempPath, path);
    }

    private IEnumerable<TextBlock> Tokenize(IEnumerable<TextBlock> blocks)
    {
        var tokenizer = new Tokenizer();
        foreach (var block in blocks)
        {
            block.Tokens.Clear();
            if (block.Text != null)
                block.Tokens.AddRange(tokenizer.Tokenize(block.Text));
            ProcessedBlocksCount++;
            yield return block;
        }
    }

    public void ComputeStats(
        IEnumerable<FileSystemInfo> inputPaths,
        string outPath)
    {
        var serializer = new TextBlockSerializer();

        long totalTokenCount = 0;
        long totalTimedTokenCount = 0;
        var totalDuration = TimeSpan.Zero;

        foreach (var inputPath in FileStoreUtil.ExpandPaths(inputPaths))
        {
            foreach (var block in serializer.Deserialize(inputPath.FullName))
            {
                totalTokenCount += block.Tokens.Count;

                if (block.Source?.Timecodes != null)
                {
                    totalTimedTokenCount += block.Tokens.Count;
                    totalDuration += block.Source.Timecodes.Duration;
                }
            }
        }

        var stats = new ContentStatistics
        {
            TotalTokenCount = totalTokenCount,
            TotalTimedTokenCount = totalTimedTokenCount,
            TotalDuration = totalDuration,
            AverageTokenDuration = totalTimedTokenCount > 0 ?
                totalDuration / totalTimedTokenCount :
                null,
        };

        var statsDoc = new ContentStatisticsDocument { Stats = stats };
        WriteYamlDocument(outPath, statsDoc);
    }

    private static void WriteYamlDocument(string path, object o)
    {
        // TODO: extract common TandokuDocumentSerializer from TextBlockSerializer

        using var writer = File.CreateText(path);
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new Yaml.FlowStyleEventEmitter(next))
            .WithEventEmitter(next => new Yaml.StringQuotingEmitter(next))
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitDefaults |
                DefaultValuesHandling.OmitEmptyCollections |
                DefaultValuesHandling.OmitNull)
            .Build();
        serializer.Serialize(writer, o);
    }
}
