// File: tests/Tandoku.Core.Tests/Subtitles/TtmlSerializerTests.cs
namespace Tandoku.Tests.Subtitles;

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Tandoku.Subtitles.Ttml;
using Xunit;

public class TtmlSerializerTests
{
    [Fact]
    public async Task Deserialize_NetflixTtml_DeepValidation()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        // Adjust the resource name as per your project structure.
        var resourceName = "Tandoku.Core.Tests.Subtitles.example-netflix.ja.ttml";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource {resourceName} not found.");

        // Act
        var doc = await TtmlSerializer.DeserializeAsync(stream);

        // Assert basic structure
        doc.Should().NotBeNull();
        doc!.Language.Should().Be("ja");
        doc.Head.Should().NotBeNull();
        doc.Body.Should().NotBeNull();
        doc.Body!.Divs.Should().NotBeNullOrEmpty();
        doc.Head!.Styling.Should().NotBeNull();
        doc.Head.Styling!.Styles.Should().NotBeNullOrEmpty();
        doc.Head.Styling.Styles.Should().Contain(s => s.Ruby != null, "at least one style should have a ruby attribute defined");

        // Deep validation on a few subtitles:
        // Check first subtitle (subtitle1) text.
        var paragraphs = doc.Body.Divs[0].Paragraphs;
        paragraphs.Should().NotBeNullOrEmpty();
        paragraphs!.Count.Should().BeGreaterThanOrEqualTo(5, "the test file contains at least 5 subtitles");

        var subtitle1 = paragraphs[0];
        var flat1 = FlattenContent(subtitle1.Content);
        flat1.Should().Contain("〈2018年 札幌〉");

        // Check subtitle3 has a line break (a TtmlBr element) and expected text fragments.
        var subtitle3 = paragraphs[2];
        var flat3 = FlattenContent(subtitle3.Content);
        flat3.Should().Contain("\n")
             .And.Contain("ジグソーパズルだ").And.Contain("〞と");

        // Check subtitle19 has two parts separated by a line break.
        var subtitle19 = paragraphs.Find(p => p?.Content?.Exists(o => o is TtmlBr) ?? false);
        subtitle19.Should().NotBeNull("subtitle19 should contain a <br/> element");
        var flat19 = FlattenContent(subtitle19!.Content);
        flat19.Should().Contain("（也英）皆様")
             .And.Contain("おはようございます")
             .And.Contain("\n");

        // Check subtitle29 for ruby content (nested spans, expected names)
        var subtitle29 = paragraphs[4]; // Here, using index 4 if there are at least 5 paragraphs; adjust index as needed.
        // For demonstration we pick the 5th subtitle if available
        subtitle29 = paragraphs.Count >= 5 ? paragraphs[4] : paragraphs[0];
        var flat29 = FlattenContent(subtitle29.Content);
        flat29.Should().Contain("野口")
             .And.Contain("のぐち")
             .And.Contain("也英でございます");

        // Also, check one subtitle that uses a music note marker
        var subtitle712 = paragraphs.Find(p => FlattenContent(p.Content).Contains("悲しいlove song"));
        subtitle712.Should().NotBeNull("a subtitle with '悲しいlove song' exists");
    }

    [Fact]
    public async Task Deserialize_AmazonTtml_DeepValidation()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        // Adjust the resource name as per your project structure.
        var resourceName = "Tandoku.Core.Tests.Subtitles.example-amazon.ja.ttml";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource {resourceName} not found.");

        // Act
        var doc = await TtmlSerializer.DeserializeAsync(stream);

        // Assert basic structure
        doc.Should().NotBeNull();
        doc!.Language.Should().Be("jp");
        doc.Head.Should().NotBeNull();
        doc.Body.Should().NotBeNull();
        doc.Body!.Divs.Should().NotBeNullOrEmpty();
        doc.Head!.Styling.Should().NotBeNull();
        doc.Head.Styling!.Styles.Should().NotBeNullOrEmpty();
        doc.Head.Styling.Styles.Should().Contain(s => s.Ruby != null, "at least one style from amazon TTML should define ruby styling");

        // Deep validation on contents:
        var paragraphs = doc.Body.Divs[0].Paragraphs;
        paragraphs.Should().NotBeNullOrEmpty();

        // Validate first paragraph text with a <br /> element.
        var p1 = paragraphs[0];
        var flat1 = FlattenContent(p1.Content);
        flat1.Should().Contain("神戸美紗").And.Contain("かんべみさ")
             .And.Contain("人生は").And.Contain("\n").And.Contain("美しいシーンで紡がれた台本だ");

        // Validate a paragraph that contains dialogue (e.g., offset from 00:01:54.240)
        var pDialogue = paragraphs.Find(p => FlattenContent(p.Content).Contains("平野友也"));
        pDialogue.Should().NotBeNull("there should be a paragraph containing dialogue, including '平野友也' and a ruby styled name");
        var flatDialogue = FlattenContent(pDialogue!.Content);
        flatDialogue.Should().Contain("みんな知ってますよね？")
             .And.Contain("平野友也")
             .And.Contain("ひらのともや");

        // Validate a paragraph from the bottom that uses a musical note marker (♪～)
        var pMusic = paragraphs.Find(p => FlattenContent(p.Content).Contains("♪"));
        pMusic.Should().NotBeNull("a subtitle with a music note should be present");
    }

    private static string FlattenContent(List<object>? content)
    {
        if (content == null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var item in content)
        {
            switch (item)
            {
                case string s:
                    sb.Append(s);
                    break;
                case TtmlSpan span:
                    sb.Append(FlattenContent(span.Content));
                    break;
                case TtmlBr:
                    sb.Append("\n");
                    break;
                default:
                    sb.Append(item.ToString());
                    break;
            }
        }
        return sb.ToString();
    }
}