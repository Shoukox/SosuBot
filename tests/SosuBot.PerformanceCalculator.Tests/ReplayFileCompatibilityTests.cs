using System.Text;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using SosuBot.Helpers;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>osu!lazer replays must reach OsuParsers in the stable shape it understands.</summary>
public sealed class ReplayFileCompatibilityTests
{
    private static readonly byte[] Tail = Encoding.ASCII.GetBytes("compressed frames and the score id");

    // The score block of a real replay exported from osu!lazer 2026.1117.0.
    private static readonly byte[] LazerScoreBlock = Convert.FromBase64String(
        "XQAAIAD8AQAAAAAAAAA9gz1B7QDP+Zy5wqoiPzNq2O+c33eovA+/4n4FScRLay0GSG4P9ilLCDqmqbexMeDmP9/VTAtRFnDkgXUQ0wYx+KdV" +
        "2eEcjlaNEiZO0D6ZUpTOHfDgl9UPWaEWo+gWQ3oyeACgqE1cfUBv+AkzDBIhPflkdAPkrO43k8F7NS3B0c+WMBbERxAbQt9e+jy7PQamFNRc" +
        "Nn8gC6e2U7lSIYMUGj4u3rQvPRuKvM1a/41HU76D6QCRL9PQge3tc4rXsalZ+3pI3zBsHTligGRKTfhspRcTwrNpP5laKagroDZGP7gRFKQQ" +
        "VpO4SAA=");

    [Fact]
    public void LazerLifeBarTimesAreRoundedAndTheRestIsKept()
    {
        // Long enough for a two-byte ULEB128 length, like real replays.
        string lifeBar = string.Concat(Enumerable.Range(0, 40)
            .Select(i => FormattableString.Invariant($"{i * 2000 + 255.63}|0.94,")));
        byte[] replay = Replay(lifeBar);

        byte[] normalized = ReplayFileCompatibility.NormalizeLifeBar(replay);

        Assert.Equal(Replay(string.Concat(Enumerable.Range(0, 40).Select(i => $"{i * 2000 + 256}|0.94,"))), normalized);
    }

    [Theory]
    [InlineData("255.63|1,2334.5|0.94,4460|1,", "256|1,2334|0.94,4460|1,")]
    [InlineData("27083|1,", "27083|1,")]
    [InlineData("", "")]
    public void OnlyDecimalTimesChange(string lifeBar, string expected) =>
        Assert.Equal(expected, ReplayFileCompatibility.NormalizeLifeBar(lifeBar));

    [Fact]
    public void StableReplaysAndBrokenFilesAreReturnedAsTheyAre()
    {
        byte[] stable = Replay("27083|1,29083|0.5,");
        byte[] noLifeBar = Replay(null);
        byte[] garbage = Encoding.ASCII.GetBytes("not a replay");

        Assert.Same(stable, ReplayFileCompatibility.NormalizeLifeBar(stable));
        Assert.Same(noLifeBar, ReplayFileCompatibility.NormalizeLifeBar(noLifeBar));
        Assert.Same(garbage, ReplayFileCompatibility.NormalizeLifeBar(garbage));
        Assert.Empty(ReplayFileCompatibility.NormalizeLifeBar([]));
    }

    [Fact]
    public void LazerReplaysCarryTheirFullResult()
    {
        ReplayFileScore score = ReplayFileCompatibility.ReadScore(FullReplay(30000019, -1, LazerScoreBlock));

        Assert.Equal(-1, score.OnlineId);
        Assert.NotNull(score.LazerScore);
        Assert.Equal(283, score.LazerScore.Statistics[HitResult.SliderTailHit]);
        Assert.Equal(292, score.LazerScore.MaximumStatistics[HitResult.SliderTailHit]);
        Assert.Equal(ScoreRank.B, score.LazerScore.Rank);
    }

    [Fact]
    public void StableReplaysOnlyKnowTheirOnlineId()
    {
        ReplayFileScore score = ReplayFileCompatibility.ReadScore(FullReplay(20230621, 4856655554, null));

        Assert.Equal(4856655554, score.OnlineId);
        Assert.Null(score.LazerScore);
    }

    [Theory]
    [InlineData(4, 0xff)] // a dictionary of gigabytes
    [InlineData(10, 0xff)] // an unpacked size of terabytes
    [InlineData(20, 0x55)] // damaged compressed data
    public void DamagedLazerBlocksAreIgnoredAndTheIdSurvives(int offset, byte value)
    {
        byte[] block = LazerScoreBlock.ToArray();
        block[offset] = value;

        ReplayFileScore score = ReplayFileCompatibility.ReadScore(FullReplay(30000019, 77, block));

        Assert.Equal(77, score.OnlineId);
        Assert.Null(score.LazerScore);
    }

    [Fact]
    public void BrokenFilesHaveNoScore()
    {
        byte[] replay = FullReplay(30000019, 77, LazerScoreBlock);

        for (var length = 0; length < replay.Length; length += 7)
            Assert.Null(ReplayFileCompatibility.ReadScore(replay[..length]).LazerScore);
        Assert.Null(ReplayFileCompatibility.ReadScore(Encoding.ASCII.GetBytes("not a replay")).LazerScore);
    }

    /// <summary>A whole .osr file: header, life bar, timestamp, frames, online id and the osu!lazer block.</summary>
    private static byte[] FullReplay(int version, long onlineId, byte[]? lazerBlock)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)0);
        writer.Write(version);
        WriteString(writer, "0123456789abcdef0123456789abcdef");
        WriteString(writer, "Guest");
        WriteString(writer, "fedcba9876543210fedcba9876543210");
        foreach (ushort count in new ushort[] { 939, 334, 13, 0, 0, 12 })
            writer.Write(count);
        writer.Write(340319);
        writer.Write((ushort)415);
        writer.Write((byte)0);
        writer.Write(0);
        WriteString(writer, "27083|1,");
        writer.Write(638000000000000000L);
        writer.Write(Tail.Length);
        writer.Write(Tail);
        writer.Write(onlineId);
        if (lazerBlock is not null)
        {
            writer.Write(lazerBlock.Length);
            writer.Write(lazerBlock);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>An .osr header up to the life bar, the life bar, then <see cref="Tail" /> for the rest.</summary>
    private static byte[] Replay(string? lifeBar)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)0);
        writer.Write(30000019);
        WriteString(writer, "0123456789abcdef0123456789abcdef");
        WriteString(writer, "Guest");
        WriteString(writer, "fedcba9876543210fedcba9876543210");
        foreach (ushort count in new ushort[] { 939, 334, 13, 0, 0, 12 })
            writer.Write(count);
        writer.Write(340319);
        writer.Write((ushort)415);
        writer.Write((byte)0);
        writer.Write(0);
        WriteString(writer, lifeBar);
        writer.Write(Tail);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteString(BinaryWriter writer, string? text)
    {
        if (text is null)
        {
            writer.Write((byte)0);
            return;
        }

        writer.Write((byte)0x0b);
        // BinaryWriter.Write(string) is a ULEB128 length followed by UTF-8, the same as osu! strings.
        writer.Write(text);
    }
}
