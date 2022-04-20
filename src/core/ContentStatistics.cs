namespace Tandoku;

public sealed class ContentStatisticsDocument
{
    public ContentStatistics Stats { get; set; }
}

public sealed class ContentStatistics
{
    public long? TotalTokenCount { get; set; }
    public long? TotalTimedTokenCount { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public TimeSpan? AverageTokenDuration { get; set; }
}
