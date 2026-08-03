namespace SosuBot.Monitoring;

public sealed record DatabaseMetricsSnapshot(
    int RegisteredUsers,
    int KnownChats,
    int KnownChatMembers,
    int DailyStatistics,
    int Scores,
    int CachedUsers,
    int TrackedChats,
    int TrackedPlayers,
    int PendingScoreDeliveries,
    int FailedScoreDeliveries,
    double OldestPendingScoreDeliveryAgeSeconds,
    int PendingDailyReportDeliveries,
    int FailedDailyReportDeliveries,
    IReadOnlyDictionary<string, int> RegisteredUsersByMode,
    IReadOnlyDictionary<string, int> ChatsByLanguage,
    IReadOnlyList<DailyStatisticMetric> LatestDailyStatistics,
    IReadOnlyList<CommandUsageMetric> CommandUsage);

public sealed record DailyStatisticMetric(
    string Country,
    DateTime DayOfStatistic,
    int ActiveUsers,
    int Scores,
    int BeatmapsPlayed);

public sealed record CommandUsageMetric(
    string Command,
    string Window,
    long SuccessCount,
    long ErrorCount,
    long CancelledCount,
    double AverageDurationSeconds);
