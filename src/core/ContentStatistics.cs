namespace Tandoku;

public sealed class ContentStatisticsDocument
{
    public ContentStatistics Stats { get; set; }

    public TermfreqStatistics Termfreq { get; set; }
}

public sealed class ContentStatistics
{
    public long? TotalTokenCount { get; set; }
    public long? TotalTimedTokenCount { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public TimeSpan? AverageTokenDuration { get; set; }
    
    public long? ProperNounTokenCount { get; set; }

    // TODO: move corpus aggregates into separate structures?
    public long? TotalCorpusItemCount { get; set; }

    // TODO: move analytics into separate structures?
    public double? UtilityScore { get; set; }
    public double? UtilityUniqueScore { get; set; }
}

public sealed class TermfreqStatistics : Dictionary<string, TermStatistics>
{
    public string GetKeyFromToken(Token token)
    {
        return $"{token.BaseForm ?? token.Term}|{token.PartOfSpeech}";
    }

    internal TermfreqStatistics CloneForSerialization(bool sortByCorpusItemCount = false)
    {
        var result = new TermfreqStatistics();
        var sorted = (sortByCorpusItemCount ?
                this.OrderByDescending(p => p.Value.CorpusItemCount) :
                this.OrderByDescending(p => p.Value.Count))
            .ThenBy(p => p.Key);

        // Note: this relies on .NET Dictionary implementation preserving order of inserts, which isn't documented behavior
        // Could replace this with an actual stable sorted dictionary implementation if needed
        foreach (var pair in sorted)
            result.Add(pair.Key, pair.Value);

        return result;
    }
}

public sealed class TermStatistics
{
    public string? PartOfSpeech { get; set; }
    public long? Count { get; set; }
    public long? CorpusItemCount { get; set; }
}
