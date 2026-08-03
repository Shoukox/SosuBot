using SosuBot.Graphics.Models;
using Xunit;

namespace SosuBot.Graphics.Tests;

public sealed class ScorePreviewBbCodeParserTests
{
    [Fact]
    public void TryParse_ParsesNestedColorAndGlowRuns()
    {
        const string input = "New [color=#f48][glow=18]top play[/glow][/color]!";

        bool parsed = ScorePreviewBbCodeParser.TryParse(input, out ScorePreviewText? text, out var error);

        Assert.True(parsed);
        Assert.Equal(ScorePreviewTextParseError.None, error);
        Assert.NotNull(text);
        Assert.Equal("New top play!", text.PlainText);
        Assert.Collection(text.Runs,
            run => Assert.Equal(new ScorePreviewTextRun("New ", "#FFFFFF", 10), run),
            run => Assert.Equal(new ScorePreviewTextRun("top play", "#FF4488", 18), run),
            run => Assert.Equal(new ScorePreviewTextRun("!", "#FFFFFF", 10), run));
    }

    [Theory]
    [InlineData("[color=nope]text[/color]", ScorePreviewTextParseError.InvalidColor)]
    [InlineData("[glow=41]text[/glow]", ScorePreviewTextParseError.InvalidGlow)]
    [InlineData("[color=red]text[/glow]", ScorePreviewTextParseError.InvalidMarkup)]
    [InlineData("[color=red]text", ScorePreviewTextParseError.InvalidMarkup)]
    public void TryParse_RejectsInvalidMarkup(string input, ScorePreviewTextParseError expectedError)
    {
        bool parsed = ScorePreviewBbCodeParser.TryParse(input, out ScorePreviewText? text, out var error);

        Assert.False(parsed);
        Assert.Null(text);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryParse_RejectsTextAboveLimit()
    {
        string input = new('a', ScorePreviewBbCodeParser.MaximumTextLength + 1);

        bool parsed = ScorePreviewBbCodeParser.TryParse(input, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(ScorePreviewTextParseError.TooLong, error);
    }
}
