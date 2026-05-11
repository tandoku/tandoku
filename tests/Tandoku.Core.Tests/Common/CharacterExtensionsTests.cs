namespace Tandoku.Tests.Common;

public class CharacterExtensionsTests
{
    [Test]
    [Arguments('a', true)]
    [Arguments('Z', true)]
    [Arguments('5', true)]
    [Arguments('_', true)]
    [Arguments('日', true)]    // Lo (Han letter)
    [Arguments('ｦ', true)]   // Lo (halfwidth katakana)
    [Arguments('\u0301', true)] // Mn (combining acute accent)
    [Arguments('\u203F', true)] // Pc (undertie)
    [Arguments(' ', false)]
    [Arguments('!', false)]
    [Arguments('-', false)]
    [Arguments('。', false)]
    public void IsRegexWordCharacter_DefaultMode(char c, bool expected)
    {
        c.IsRegexWordCharacter().Should().Be(expected);
    }

    [Test]
    [Arguments('a', true)]
    [Arguments('5', true)]
    [Arguments('_', true)]
    // EcmaScript mode currently delegates to char.IsLetterOrDigit, so non-ASCII
    // letters/digits still match. (Differs from JavaScript \w but matches current code.)
    [Arguments('日', true)]
    [Arguments('\u0301', false)] // Mn (combining acute accent)
    [Arguments('\u203F', false)] // Pc (undertie)
    [Arguments('-', false)]
    public void IsRegexWordCharacter_EcmaScriptMode(char c, bool expected)
    {
        c.IsRegexWordCharacter(ecmaScript: true).Should().Be(expected);
    }
}
