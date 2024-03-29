﻿namespace Tandoku;

using MathNet.Numerics.Statistics;

public sealed class StatsProcessor
{
    public void ComputeStats(
        IEnumerable<FileSystemInfo> inputPaths,
        string outPath)
    {
        var contentSerializer = new TextBlockSerializer();

        var statsAccumulator = new ContentStatsAccumulator();
        var termfreqAccumulator = new ContentTermfreqAccumulator();
        var accumulators = new Accumulator<TextBlock>[]
        {
            statsAccumulator,
            termfreqAccumulator
        };

        foreach (var inputPath in FileStoreUtil.ExpandPaths(inputPaths))
        {
            foreach (var block in contentSerializer.Deserialize(inputPath.FullName))
            {
                // TODO: generalize filters, move this to accumulators ?
                if (block.ContentKind != ContentKind.Primary)
                    continue;

                foreach (var accumulator in accumulators)
                    accumulator.Accumulate(block);
            }
        }

        var statsDoc = new ContentStatisticsDocument
        {
            Stats = statsAccumulator.ToResult(),
            Termfreq = termfreqAccumulator.ToResult(),
        };

        var statsSerializer = new SingleDocumentSerializer<ContentStatisticsDocument>();
        statsSerializer.SerializeYaml(outPath, statsDoc);
    }

    public void ComputeAggregates(
        IEnumerable<FileSystemInfo> inputPaths,
        string outPath)
    {
        var statsSerializer = new SingleDocumentSerializer<ContentStatisticsDocument>();

        var statsAccumulator = new AggregateStatsAccumulator();
        var termfreqAccumulator = new AggregateTermfreqAccumulator();
        var accumulators = new Accumulator<ContentStatisticsDocument>[]
        {
            statsAccumulator,
            termfreqAccumulator
        };

        foreach (var inputPath in FileStoreUtil.ExpandPaths(inputPaths))
        {
            var statsDoc = statsSerializer.DeserializeYaml(inputPath.FullName);

            foreach (var accumulator in accumulators)
                accumulator.Accumulate(statsDoc);
        }

        var aggDoc = new ContentStatisticsDocument
        {
            Stats = statsAccumulator.ToResult(),
            Termfreq = termfreqAccumulator.ToResult(),
        };

        statsSerializer.SerializeYaml(outPath, aggDoc);
    }

    public void ComputeAnalytics(
        IEnumerable<FileSystemInfo> inputPaths,
        string corpusAggregatesPath)
    {
        var statsSerializer = new SingleDocumentSerializer<ContentStatisticsDocument>();

        var aggDoc = statsSerializer.DeserializeYaml(corpusAggregatesPath);

        foreach (var inputPath in FileStoreUtil.ExpandPaths(inputPaths))
        {
            var doc = statsSerializer.DeserializeYaml(inputPath.FullName);

            (doc.Stats.UtilityScore, doc.Stats.UtilityUniqueScore) =
                ComputeUtilityScores(doc, aggDoc);

            statsSerializer.SerializeYaml(inputPath.FullName, doc);
        }
    }

    private (double utilityScore, double utilityUniqueScore) ComputeUtilityScores(
        ContentStatisticsDocument doc,
        ContentStatisticsDocument aggDoc)
    {
        double totalCorpusItemCount = (double)aggDoc.Stats.TotalCorpusItemCount;
        var corpusTermFreq = aggDoc.Termfreq;

        double utilityNumerator = 0.0;
        double utilityDenominator = 0.0;

        // TODO: this score is currently heavily biased by the amount of content in a volume
        // need to reconsider what I'm trying to calculate here and how to normalize it
        double utilityUniqueNumerator = 0.0;
        double utilityUniqueDenominator = 0.0;

        foreach (var entry in doc.Termfreq)
        {
            if (PartOfSpeechUtil.IsProperNoun(entry.Value.PartOfSpeech))
                continue;

            var corpusEntry = corpusTermFreq[entry.Key];
            double entryUtility = (double)corpusEntry.CorpusItemCount / totalCorpusItemCount;

            double weight = (double)entry.Value.Count;

            utilityNumerator += (entryUtility * weight);
            utilityDenominator += weight;

            utilityUniqueNumerator += entryUtility;
            utilityUniqueDenominator += 1;
        }

        return (utilityScore: utilityNumerator / utilityDenominator,
            utilityUniqueScore: utilityUniqueNumerator / utilityUniqueDenominator);
    }

    private abstract class Accumulator<T>
    {
        public abstract void Accumulate(T block);
    }

    private sealed class ContentStatsAccumulator : Accumulator<TextBlock>
    {
        private long totalTokenCount = 0;
        private long totalTimedTokenCount = 0;
        private TimeSpan totalDuration = TimeSpan.Zero;

        // TODO: switch to using subtitle ordinals
        // later could revert to this simpler implementation when switching to composite blocks for split subtitles
        //private readonly List<TimeSpan> blockAverageTokenDurations = new List<TimeSpan>();
        private readonly Dictionary<TimecodePair, int> tokenCountByTimecode = new();

        private long properNounTokenCount = 0;

        public override void Accumulate(TextBlock block)
        {
            // Exclude blocks without any tokens from statistics (e.g. exclude for total/average durations)
            if (block.Tokens.Count == 0)
                return;

            totalTokenCount += block.Tokens.Count;

            if (block.Source?.Timecodes != null)
            {
                totalTimedTokenCount += block.Tokens.Count;
                if (!tokenCountByTimecode.ContainsKey(block.Source.Timecodes.Value))
                    totalDuration += block.Source.Timecodes.Value.Duration;

                //blockAverageTokenDurations.Add(
                //    block.Source.Timecodes.Value.Duration / block.Tokens.Count);
                if (!tokenCountByTimecode.TryGetValue(block.Source.Timecodes.Value, out var tokenCount))
                    tokenCount = 0;
                tokenCount += block.Tokens.Count;
                tokenCountByTimecode[block.Source.Timecodes.Value] = tokenCount;
            }

            properNounTokenCount += block.Tokens.Count(
                t => PartOfSpeechUtil.IsProperNoun(t.PartOfSpeech));
        }

        public ContentStatistics ToResult()
        {
            return new ContentStatistics
            {
                TotalTokenCount = totalTokenCount,
                TotalTimedTokenCount = totalTimedTokenCount,
                TotalDuration = totalDuration,
                AverageTokenDuration = totalTimedTokenCount > 0 ?
                    totalDuration / totalTimedTokenCount :
                    null,
                MedianTokenDurationByBlock = TimeSpan.FromSeconds(
                    //blockAverageTokenDurations.Select(t => t.TotalSeconds).Median()),
                    tokenCountByTimecode.Select(p => p.Key.Duration.TotalSeconds / p.Value).Median()),
                ProperNounTokenCount = properNounTokenCount,
            };
        }
    }

    private sealed class ContentTermfreqAccumulator : Accumulator<TextBlock>
    {
        private readonly TermfreqStatistics termfreq = new TermfreqStatistics();

        public override void Accumulate(TextBlock block)
        {
            foreach (var token in block.Tokens)
            {
                var key = termfreq.GetKeyFromToken(token);
                if (termfreq.TryGetValue(key, out var termStats))
                {
                    termStats.Count += 1;
                }
                else
                {
                    termStats = new TermStatistics
                    {
                        PartOfSpeech = token.PartOfSpeech,
                        Count = 1,
                    };
                    termfreq.Add(key, termStats);
                }
            }
        }

        public TermfreqStatistics ToResult()
        {
            return termfreq.CloneForSerialization();
        }
    }

    private sealed class AggregateStatsAccumulator : Accumulator<ContentStatisticsDocument>
    {
        private long totalCorpusItemCount = 0;

        public override void Accumulate(ContentStatisticsDocument stats)
        {
            totalCorpusItemCount++;
        }

        public ContentStatistics ToResult()
        {
            return new ContentStatistics
            {
                TotalCorpusItemCount = totalCorpusItemCount,
            };
        }
    }

    private sealed class AggregateTermfreqAccumulator : Accumulator<ContentStatisticsDocument>
    {
        private readonly TermfreqStatistics termfreq = new TermfreqStatistics();

        public override void Accumulate(ContentStatisticsDocument stats)
        {
            foreach (var entry in stats.Termfreq)
            {
                var key = entry.Key;
                if (termfreq.TryGetValue(key, out var termStats))
                {
                    termStats.Count += entry.Value.Count;
                    termStats.CorpusItemCount += 1;
                }
                else
                {
                    termStats = new TermStatistics
                    {
                        PartOfSpeech = entry.Value.PartOfSpeech,
                        Count = entry.Value.Count,
                        CorpusItemCount = 1,
                    };
                    termfreq.Add(key, termStats);
                }
            }
        }

        public TermfreqStatistics ToResult()
        {
            return termfreq.CloneForSerialization(sortByCorpusItemCount: true);
        }
    }
}
