namespace Tandoku;

using System.Globalization;

internal static class CharacterExtensions
{
    /// <summary>
    /// Determines whether the specified character is a "word" character as defined by regular expressions (\w character class).
    /// </summary>
    /// <remarks>See https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-classes-in-regular-expressions#word-character-w</remarks>
    internal static bool IsRegexWordCharacter(this char c, bool ecmaScript = false)
    {
        if (ecmaScript)
            return char.IsLetterOrDigit(c) || c == '_';

        // Covers Ll, Lu, Lt, Lo, Lm, Nd
        if (char.IsLetterOrDigit(c))
            return true;

        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        return cat is UnicodeCategory.NonSpacingMark or // Mn (combining marks)
            UnicodeCategory.ConnectorPunctuation;       // Pc (e.g. _, undertie, fullwidth low line)
    }
}
