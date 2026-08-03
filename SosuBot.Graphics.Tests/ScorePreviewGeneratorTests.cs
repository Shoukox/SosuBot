using SosuBot.Graphics.Models;
using SixLabors.ImageSharp;
using Xunit;

namespace SosuBot.Graphics.Tests;

public sealed class ScorePreviewGeneratorTests
{
    [Fact]
    public void Generate_CreatesFullHdPngFromEmbeddedAssets()
    {
        Assert.True(ScorePreviewBbCodeParser.TryParse(
            "[color=#ff4d8d][glow=18]New top play![/glow][/color]",
            out ScorePreviewText? text,
            out _));
        ScorePreviewGenerator generator = new();

        using MemoryStream preview = generator.Generate(new ScorePreviewData
        {
            BeatmapTitle = "Artist - Beatmap",
            DifficultyName = "Insane",
            Username = "Player",
            Rank = "S",
            CountryCode = "UZ",
            CountryRank = 42,
            IsFullCombo = true,
            Misses = 0,
            PerformancePoints = 321.4,
            Combo = 1234,
            AccuracyPercent = 99.27,
            StarRating = 6.42,
            Bpm = 190,
            Mods = ["HD", "DT"]
        }, text!);

        ImageInfo info = Image.Identify(preview);

        Assert.Equal(ScorePreviewGenerator.PreviewWidth, info.Width);
        Assert.Equal(ScorePreviewGenerator.PreviewHeight, info.Height);
    }
}
