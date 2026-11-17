using System.Text;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using SosuBot.Services;
using Telegram.Bot.Types;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>Guards the memory bounds and the osu!-server parity of the official calculator.</summary>
public sealed class PPCalculationCacheTests
{
    private static readonly byte[] Beatmap = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "testdata", "native-fixture.osu"));

    [Fact]
    public async Task CacheNeverHoldsMoreEntriesThanItsLimit()
    {
        using var calculator = new PPCalculator(new PPCalculationCacheOptions { MaxEntries = 2 });
        Mod[][] modSets =
        [
            [],
            [new OsuModHardRock()],
            [new OsuModDoubleTime()],
            [new OsuModHidden()],
            [new OsuModEasy()]
        ];

        foreach (Mod[] mods in modSets)
        {
            PPCalculationResult? result = await calculator.CalculatePpAsync(
                beatmapId: 1,
                beatmapFile: new MemoryStream(Beatmap, writable: false),
                accuracy: 1,
                scoreMods: mods,
                cancellationToken: CancellationToken.None);
            Assert.NotNull(result);
        }

        Assert.True(calculator.CachedBeatmapCount <= 2, $"{calculator.CachedBeatmapCount} cached entries");
    }

    [Fact]
    public async Task UpdatedBeatmapUnderTheSameIdIsNotServedFromCache()
    {
        using var calculator = new PPCalculator();
        string text = Encoding.UTF8.GetString(Beatmap).TrimEnd();
        byte[] updated = Encoding.UTF8.GetBytes(text[..text.LastIndexOf('\n')]);

        PPCalculationResult? original = await calculator.CalculatePpAsync(
            beatmapId: 7, beatmapFile: new MemoryStream(Beatmap, writable: false), accuracy: 1,
            cancellationToken: CancellationToken.None);
        PPCalculationResult? afterUpdate = await calculator.CalculatePpAsync(
            beatmapId: 7, beatmapFile: new MemoryStream(updated, writable: false), accuracy: 1,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(original);
        Assert.NotNull(afterUpdate);
        Assert.Equal(original!.BeatmapHitObjectsCount - 1, afterUpdate!.BeatmapHitObjectsCount);
    }

    /// <summary>The fixture repeated 30 times: long enough for osu!'s miss estimates to tell apart.</summary>
    private static byte[] LongBeatmap()
    {
        string text = Encoding.UTF8.GetString(Beatmap).Replace("\r\n", "\n").TrimEnd();
        int objectsStart = text.IndexOf("[HitObjects]", StringComparison.Ordinal) + "[HitObjects]".Length;
        string[] objects = text[objectsStart..].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder(text[..objectsStart]).Append('\n');
        for (int repeat = 0; repeat < 30; repeat++)
        {
            foreach (string hitObject in objects)
            {
                string[] fields = hitObject.Split(',');
                fields[2] = (int.Parse(fields[2]) + repeat * 10_000).ToString();
                result.Append(string.Join(',', fields)).Append('\n');
            }
        }

        return Encoding.UTF8.GetBytes(result.ToString());
    }

    [Fact]
    public async Task SubmittedScoreKeepsItsStatisticsAndUsesLegacyTotalScore()
    {
        using var calculator = new PPCalculator();
        byte[] beatmap = LongBeatmap();
        PPCalculationResult perfect = (await calculator.CalculatePpAsync(
            beatmapId: 1, beatmapFile: new MemoryStream(beatmap, writable: false), accuracy: 1,
            scoreMods: [new OsuModClassic()], cancellationToken: CancellationToken.None))!;
        int objects = perfect.BeatmapHitObjectsCount;
        var statistics = new Dictionary<HitResult, int>
        {
            // osu! never estimates more misses than non-300 judgements, so leave it room.
            [HitResult.Great] = objects - 60,
            [HitResult.Ok] = 60
        };
        var maximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = objects };

        Task<PPCalculationResult?> CalculateStableScore(long? legacyTotalScore) => calculator.CalculatePpAsync(
            beatmapId: 1,
            beatmapFile: new MemoryStream(beatmap, writable: false),
            scoreMaxCombo: perfect.BeatmapMaxCombo / 2,
            scoreMods: [new OsuModClassic()],
            scoreStatistics: statistics,
            cancellationToken: CancellationToken.None,
            maximumStatistics: maximumStatistics,
            legacyTotalScore: legacyTotalScore);

        PPCalculationResult? withoutLegacyScore = await CalculateStableScore(null);
        // A legacy score below what the max combo alone gives makes osu! assume the harshest miss count.
        PPCalculationResult? withLegacyScore = await CalculateStableScore(1);

        Assert.NotNull(withoutLegacyScore);
        Assert.NotNull(withLegacyScore);
        Assert.Equal(statistics, withLegacyScore!.ScoreStatistics);
        // osu!'s accuracy: 300 per great and 100 per ok over 300 per object.
        Assert.Equal((300.0 * (objects - 60) + 100 * 60) / (300.0 * objects), withLegacyScore.CalculatedAccuracy, 12);
        // osu! estimates slider breaks of stable scores from their legacy total score.
        Assert.True(withLegacyScore.PP < withoutLegacyScore!.PP,
            $"{withLegacyScore.PP} with legacy score, {withoutLegacyScore.PP} without");
    }

    [Fact]
    public void UserDirectoryForgetsLeastRecentlySeenUsers()
    {
        var directory = new TelegramUserDirectory();

        for (long id = 1; id <= TelegramUserDirectory.MaxUsers + 1; id++)
            directory.Remember(new User { Id = id, Username = $"user{id}" });

        Assert.True(directory.Count <= TelegramUserDirectory.MaxUsers, $"{directory.Count} users");
        Assert.False(directory.TryGetUserId("user1", out _));
        Assert.True(directory.TryGetUserId($"user{TelegramUserDirectory.MaxUsers + 1}", out long latest));
        Assert.Equal(TelegramUserDirectory.MaxUsers + 1, latest);
    }
}
