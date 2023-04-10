namespace Tandoku;

internal static class PartOfSpeechUtil
{
    internal static bool IsProperNoun(string? pos) => pos?.StartsWith("名詞-固有名詞-", StringComparison.Ordinal) == true;
}
