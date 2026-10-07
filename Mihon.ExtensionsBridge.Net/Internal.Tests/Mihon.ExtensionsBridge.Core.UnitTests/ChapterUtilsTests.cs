using Mihon.ExtensionsBridge.Core.Utilities;
using Mihon.ExtensionsBridge.Models.Extensions;
using Xunit;

namespace Mihon.ExtensionsBridge.Core.UnitTests;

public class ChapterUtilsTests
{
    [Theory]
    [InlineData("", "Capitolo 08.5", 8f, "8.5")]
    [InlineData("", "Capitolo 210.5 - Extra", 210f, "210.5")]
    [InlineData("", "Capitolo 00.5", 0f, "0.5")]
    [InlineData("", "Capitolo 8,5", 8f, "8.5")]
    [InlineData("", "Capitolo 08", 8f, "8")]
    [InlineData("", "Capitolo 26.5", 26.5f, "26.5")]
    [InlineData("", "Chapter 8: The 8.5 Mile Road", 8f, "8")]
    [InlineData("", "Capitolo 9.5", 10f, "10")]
    [InlineData("", "Bonus", 8f, "8")]
    [InlineData("", "Capitolo 8.5", -2f, "-2")]
    [InlineData("", "Chapter 8.5", -0.5f, "-0.5")]
    [InlineData("Series 210.5", "Series 210.5 Ch. 8", 8f, "8")]
    [InlineData("Series 210.5", "Series 210.5 Ch. 8.5", 8f, "8.5")]
    [InlineData("", "Vol. 2 Ch. 8.5", 8f, "8.5")]
    [InlineData("", "Chapter 8 extra", 8f, "8")]
    [InlineData("", "Chapter 8 omake", 8f, "8")]
    [InlineData("", "Chapter 8 special", 8f, "8")]
    [InlineData("", "Chapter 8a", 8f, "8")]
    [InlineData("", "Chapter 8-5", 8f, "8")]
    [InlineData("", "Chapter 999999999999999999999999999999999999999.5", 8f, "8")]
    [InlineData("", "Chapter 8.00001", 8f, "8.00001")]
    public void KnownNumbersOnlyAdoptExplicitMatchingFractions(string title, string name,
        float number, string expected)
    {
        var chapter = new ParsedChapter { Name = name, ChapterNumber = number };
        chapter.ParsedNumber = ChapterUtils.ParseChapterNumber(title, chapter.Name, chapter.ChapterNumber);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), chapter.ParsedNumber);
    }

    [Theory]
    [InlineData("Capitolo 08.5", "8.5")]
    [InlineData("Chapter 8 extra", "8.99")]
    [InlineData("Chapter 8a", "8.1")]
    [InlineData("Chapter 8-5", "8.5")]
    [InlineData("Bonus", "-1")]
    public void UnknownNumberFallbackIsUnchanged(string name, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            ChapterUtils.ParseChapterNumber("", name));
    }

    [Fact]
    public void WholeAndHalfChaptersProduceSeparateDecimalKeys()
    {
        var chapters = new[]
        {
            new ParsedChapter { Name = "Capitolo 08", ChapterNumber = 8f },
            new ParsedChapter { Name = "Capitolo 08.5", ChapterNumber = 8f }
        };
        foreach (var chapter in chapters)
            chapter.ParsedNumber = ChapterUtils.ParseChapterNumber("Grand Blue", chapter.Name, chapter.ChapterNumber);

        var byNumber = chapters.ToDictionary(chapter => chapter.ParsedNumber);
        Assert.Equal(2, byNumber.Count);
        Assert.Equal("Capitolo 08", byNumber[8m].Name);
        Assert.Equal("Capitolo 08.5", byNumber[8.5m].Name);
    }
}
