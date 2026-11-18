using System.Globalization;
using System.Text;
using osu.Game.Rulesets.Osu.Mods;
using SosuBot.Database.Models;
using SosuBot.Services.Ai;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class BeatmapPatternAnalyzerTests
{
    private const double BeatLength = 60_000 / 180.0;

    /// <summary>180 BPM: a 16-note stream at 1/4, a pause, then at 0:40 ten notes jumping 312px apart at 1/2.</summary>
    private static MemoryStream StreamThenJumps()
    {
        var map = new StringBuilder("""
            osu file format v14

            [General]
            Mode: 0

            [Difficulty]
            HPDrainRate:5
            CircleSize:4
            OverallDifficulty:8
            ApproachRate:9
            SliderMultiplier:1.4
            SliderTickRate:1

            [TimingPoints]
            0,333.333333333333,4,2,0,100,1,0

            [HitObjects]

            """);
        for (var i = 0; i < 16; i++)
            map.AppendLine(Circle(100 + i * 20, 192, 1_000 + i * BeatLength / 4));
        for (var i = 0; i < 10; i++)
            map.AppendLine(Circle(i % 2 == 0 ? 100 : 412, 192, 40_000 + i * BeatLength / 2));
        return new MemoryStream(Encoding.UTF8.GetBytes(map.ToString()));

        static string Circle(int x, int y, double time) =>
            string.Create(CultureInfo.InvariantCulture, $"{x},{y},{(int)Math.Round(time)},1,0,0:0:0:0:");
    }

    [Fact]
    public void FindsTheStreamAndTheJumps()
    {
        BeatmapPatterns patterns = BeatmapPatternAnalyzer.Analyze(StreamThenJumps(), Playmode.Osu, []);

        StandardPatterns standard = patterns.Standard!;
        Assert.Equal(180, patterns.Bpm.Main);
        Assert.Equal(1, standard.Streams);
        Assert.Equal(16, standard.LongestStream);
        Assert.Equal(180, standard.StreamBpm);
        Assert.Equal("stacked", standard.StreamSpacing);
        Assert.Equal(9, standard.Jumps);
        Assert.Equal(312, standard.MaxJumpPx);
        // Left, right, left: every jump turns straight back.
        Assert.Equal(100, standard.SharpJumpAnglePercent);
        Assert.Contains("1/4", patterns.RhythmPercent.Keys);
        Assert.Contains("1/2", patterns.RhythmPercent.Keys);
        Assert.Contains(patterns.Sections, section => section.Kind == "streams");
        Assert.Contains(patterns.Sections, section => section.Kind == "jumps");
    }

    [Fact]
    public void DoubleTimeSpeedsTheMapUp()
    {
        BeatmapPatterns patterns = BeatmapPatternAnalyzer.Analyze(StreamThenJumps(), Playmode.Osu, [new OsuModDoubleTime()]);

        Assert.Equal(1.5, patterns.Rate);
        Assert.Equal(270, patterns.Bpm.Main);
        Assert.Equal(270, patterns.Standard!.StreamBpm);
    }
}
