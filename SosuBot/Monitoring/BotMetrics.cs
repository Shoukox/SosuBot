using System.Collections.Concurrent;
using Prometheus;
using Telegram.Bot.Types;

namespace SosuBot.Monitoring;

public sealed class BotMetrics
{
    private static readonly TimeSpan MaximumActivityWindow = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<long, DateTimeOffset> _lastUserActivity = new();

    private readonly Counter _httpRequests = Metrics.CreateCounter(
        "sosubot_http_client_requests_total",
        "Number of outgoing HTTP requests.",
        ["client", "method", "host", "status"]);

    private readonly Histogram _httpDuration = Metrics.CreateHistogram(
        "sosubot_http_client_request_duration_seconds",
        "Duration of outgoing HTTP requests.",
        new HistogramConfiguration
        {
            LabelNames = ["client", "method", "host"],
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 15)
        });

    private readonly Gauge _httpInFlight = Metrics.CreateGauge(
        "sosubot_http_client_requests_in_flight",
        "Number of outgoing HTTP requests currently in progress.",
        ["client"]);

    private readonly Counter _updatesReceived = Metrics.CreateCounter(
        "sosubot_telegram_updates_received_total",
        "Number of Telegram updates received.",
        ["type"]);

    private readonly Counter _updatesProcessed = Metrics.CreateCounter(
        "sosubot_telegram_updates_processed_total",
        "Number of Telegram updates processed.",
        ["type", "status"]);

    private readonly Histogram _updateDuration = Metrics.CreateHistogram(
        "sosubot_telegram_update_duration_seconds",
        "Duration of Telegram update processing.",
        new HistogramConfiguration
        {
            LabelNames = ["type"],
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 16)
        });

    private readonly Gauge _updateQueueDepth = Metrics.CreateGauge(
        "sosubot_telegram_update_queue_depth",
        "Number of Telegram updates waiting to be processed.");

    private readonly Gauge _activeUsers = Metrics.CreateGauge(
        "sosubot_active_users",
        "Number of unique Telegram users active in a rolling time window.",
        ["window"]);

    private readonly Gauge _registeredUsers = Metrics.CreateGauge(
        "sosubot_registered_users",
        "Number of registered osu! users in the database.");

    private readonly Gauge _knownChats = Metrics.CreateGauge(
        "sosubot_known_chats",
        "Number of Telegram chats known to the bot.");

    private readonly Gauge _knownChatMembers = Metrics.CreateGauge(
        "sosubot_known_chat_members",
        "Number of known Telegram chat member assignments.");

    private readonly Gauge _cachedBeatmapFiles = Metrics.CreateGauge(
        "sosubot_cached_beatmap_files",
        "Number of .osu files found in the configured beatmap cache.");

    private readonly Gauge _chatsByLanguage = Metrics.CreateGauge(
        "sosubot_chats_by_language",
        "Number of known Telegram chats grouped by configured language.",
        ["language"]);

    private readonly Counter _commandsExecuted = Metrics.CreateCounter(
        "sosubot_commands_executed_total",
        "Number of bot commands executed since process start.",
        ["command", "status"]);

    private readonly Histogram _commandDuration = Metrics.CreateHistogram(
        "sosubot_command_duration_seconds",
        "Duration of bot command execution.",
        new HistogramConfiguration
        {
            LabelNames = ["command", "status"],
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 18)
        });

    private readonly Counter _commandPersistenceFailures = Metrics.CreateCounter(
        "sosubot_command_usage_persistence_failures_total",
        "Number of command usage aggregates that could not be persisted.");

    private readonly Gauge _persistedCommandUsage = Metrics.CreateGauge(
        "sosubot_persisted_command_usage",
        "Persisted command execution count loaded from PostgreSQL.",
        ["command", "status", "window"]);

    private readonly Gauge _persistedCommandAverageDuration = Metrics.CreateGauge(
        "sosubot_persisted_command_average_duration_seconds",
        "Average persisted command duration loaded from PostgreSQL.",
        ["command", "window"]);

    private readonly Gauge _registeredUsersByMode = Metrics.CreateGauge(
        "sosubot_registered_users_by_mode",
        "Number of registered users grouped by osu! mode.",
        ["mode"]);

    private readonly Gauge _databaseEntities = Metrics.CreateGauge(
        "sosubot_database_entities",
        "Number of persisted entities grouped by entity type.",
        ["entity"]);

    private readonly Gauge _trackedChats = Metrics.CreateGauge(
        "sosubot_tracked_chats",
        "Number of chats with score tracking enabled.");

    private readonly Gauge _trackedPlayers = Metrics.CreateGauge(
        "sosubot_tracked_players",
        "Number of tracked player assignments across chats.");

    private readonly Gauge _scoreDeliveryQueue = Metrics.CreateGauge(
        "sosubot_score_delivery_queue",
        "Number of tracked-score delivery rows grouped by state.",
        ["state"]);

    private readonly Gauge _oldestPendingScoreDeliveryAge = Metrics.CreateGauge(
        "sosubot_score_delivery_oldest_age_seconds",
        "Age of the oldest pending tracked-score delivery.");

    private readonly Gauge _dailyReportDeliveryQueue = Metrics.CreateGauge(
        "sosubot_daily_report_delivery_queue",
        "Number of daily-report delivery rows grouped by state.",
        ["state"]);

    private readonly Gauge _latestDailyStatistics = Metrics.CreateGauge(
        "sosubot_latest_daily_statistic",
        "Values from the latest persisted daily statistic by country.",
        ["country", "metric"]);

    private readonly Gauge _latestDailyStatisticsTimestamp = Metrics.CreateGauge(
        "sosubot_latest_daily_statistic_unixtime",
        "Unix timestamp of the latest persisted daily statistic by country.",
        ["country"]);

    private readonly Histogram _databaseSnapshotDuration = Metrics.CreateHistogram(
        "sosubot_database_metrics_refresh_duration_seconds",
        "Duration of a database-backed metrics refresh.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 14)
        });

    private readonly Counter _databaseSnapshotFailures = Metrics.CreateCounter(
        "sosubot_database_metrics_refresh_failures_total",
        "Number of failed database-backed metrics refreshes.");

    private readonly Gauge _databaseSnapshotLastSuccess = Metrics.CreateGauge(
        "sosubot_database_metrics_last_success_unixtime",
        "Unix timestamp of the last successful database-backed metrics refresh.");

    public IDisposable TrackHttpRequest(string clientName)
    {
        _httpInFlight.WithLabels(clientName).Inc();
        return new InFlightLease(_httpInFlight.WithLabels(clientName));
    }

    public void RecordHttpRequest(string clientName, HttpRequestMessage request, string status, double elapsedSeconds)
    {
        string method = request.Method.Method.ToUpperInvariant();
        string host = request.RequestUri?.Host ?? "unknown";
        _httpRequests.WithLabels(clientName, method, host, status).Inc();
        _httpDuration.WithLabels(clientName, method, host).Observe(elapsedSeconds);
    }

    public void RecordUpdateReceived(Update update)
    {
        _updatesReceived.WithLabels(GetUpdateType(update)).Inc();

        long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From.Id;
        if (userId.HasValue)
            _lastUserActivity[userId.Value] = DateTimeOffset.UtcNow;
    }

    public void RecordUpdateProcessed(Update update, string status, double elapsedSeconds)
    {
        string updateType = GetUpdateType(update);
        _updatesProcessed.WithLabels(updateType, status).Inc();
        _updateDuration.WithLabels(updateType).Observe(elapsedSeconds);
    }

    public void UpdateQueued() => _updateQueueDepth.Inc();

    public void UpdateDequeued() => _updateQueueDepth.Dec();

    public void RecordCommandExecution(string command, string status, double elapsedSeconds)
    {
        _commandsExecuted.WithLabels(command, status).Inc();
        _commandDuration.WithLabels(command, status).Observe(elapsedSeconds);
    }

    public void RecordCommandPersistenceFailure() => _commandPersistenceFailures.Inc();

    public void SetCachedBeatmapFiles(long count) => _cachedBeatmapFiles.Set(count);

    public void RefreshActiveUsers(DateTimeOffset now)
    {
        DateTimeOffset oldestAllowed = now - MaximumActivityWindow;
        foreach ((long userId, DateTimeOffset lastSeen) in _lastUserActivity)
        {
            if (lastSeen < oldestAllowed)
                _lastUserActivity.TryRemove(new KeyValuePair<long, DateTimeOffset>(userId, lastSeen));
        }

        DateTimeOffset fiveMinutesAgo = now - TimeSpan.FromMinutes(5);
        DateTimeOffset oneHourAgo = now - TimeSpan.FromHours(1);
        int active5Minutes = 0;
        int active1Hour = 0;
        int active24Hours = 0;

        foreach (DateTimeOffset lastSeen in _lastUserActivity.Values)
        {
            if (lastSeen >= oldestAllowed) active24Hours++;
            if (lastSeen >= oneHourAgo) active1Hour++;
            if (lastSeen >= fiveMinutesAgo) active5Minutes++;
        }

        _activeUsers.WithLabels("5m").Set(active5Minutes);
        _activeUsers.WithLabels("1h").Set(active1Hour);
        _activeUsers.WithLabels("24h").Set(active24Hours);
    }

    public void SetDatabaseMetrics(DatabaseMetricsSnapshot snapshot)
    {
        _registeredUsers.Set(snapshot.RegisteredUsers);
        _knownChats.Set(snapshot.KnownChats);
        _knownChatMembers.Set(snapshot.KnownChatMembers);
        _trackedChats.Set(snapshot.TrackedChats);
        _trackedPlayers.Set(snapshot.TrackedPlayers);
        _scoreDeliveryQueue.WithLabels("pending").Set(snapshot.PendingScoreDeliveries);
        _scoreDeliveryQueue.WithLabels("failed").Set(snapshot.FailedScoreDeliveries);
        _oldestPendingScoreDeliveryAge.Set(snapshot.OldestPendingScoreDeliveryAgeSeconds);
        _dailyReportDeliveryQueue.WithLabels("pending").Set(snapshot.PendingDailyReportDeliveries);
        _dailyReportDeliveryQueue.WithLabels("failed").Set(snapshot.FailedDailyReportDeliveries);

        _databaseEntities.WithLabels("registered_users").Set(snapshot.RegisteredUsers);
        _databaseEntities.WithLabels("telegram_chats").Set(snapshot.KnownChats);
        _databaseEntities.WithLabels("daily_statistics").Set(snapshot.DailyStatistics);
        _databaseEntities.WithLabels("scores").Set(snapshot.Scores);
        _databaseEntities.WithLabels("cached_users").Set(snapshot.CachedUsers);

        foreach (string mode in new[] { "osu", "taiko", "catch", "mania", "unknown" })
            _registeredUsersByMode.WithLabels(mode).Set(0);
        foreach ((string mode, int count) in snapshot.RegisteredUsersByMode)
            _registeredUsersByMode.WithLabels(mode).Set(count);

        foreach (string language in new[] { "ru", "en", "de", "other" })
            _chatsByLanguage.WithLabels(language).Set(
                snapshot.ChatsByLanguage.GetValueOrDefault(language));

        foreach (DailyStatisticMetric dailyStatistic in snapshot.LatestDailyStatistics)
        {
            _latestDailyStatistics.WithLabels(dailyStatistic.Country, "active_users").Set(dailyStatistic.ActiveUsers);
            _latestDailyStatistics.WithLabels(dailyStatistic.Country, "scores").Set(dailyStatistic.Scores);
            _latestDailyStatistics.WithLabels(dailyStatistic.Country, "beatmaps_played").Set(dailyStatistic.BeatmapsPlayed);
            _latestDailyStatisticsTimestamp.WithLabels(dailyStatistic.Country)
                .Set(new DateTimeOffset(
                    DateTime.SpecifyKind(dailyStatistic.DayOfStatistic, DateTimeKind.Utc)).ToUnixTimeSeconds());
        }

        string[] windows = ["5m", "1h", "24h", "7d", "all"];
        foreach (string command in snapshot.CommandUsage.Select(usage => usage.Command).Distinct())
        {
            foreach (string window in windows)
            {
                _persistedCommandUsage.WithLabels(command, "success", window).Set(0);
                _persistedCommandUsage.WithLabels(command, "error", window).Set(0);
                _persistedCommandUsage.WithLabels(command, "cancelled", window).Set(0);
                _persistedCommandAverageDuration.WithLabels(command, window).Set(0);
            }
        }

        foreach (CommandUsageMetric commandUsage in snapshot.CommandUsage)
        {
            _persistedCommandUsage.WithLabels(commandUsage.Command, "success", commandUsage.Window)
                .Set(commandUsage.SuccessCount);
            _persistedCommandUsage.WithLabels(commandUsage.Command, "error", commandUsage.Window)
                .Set(commandUsage.ErrorCount);
            _persistedCommandUsage.WithLabels(commandUsage.Command, "cancelled", commandUsage.Window)
                .Set(commandUsage.CancelledCount);
            _persistedCommandAverageDuration.WithLabels(commandUsage.Command, commandUsage.Window)
                .Set(commandUsage.AverageDurationSeconds);
        }
    }

    public void RecordDatabaseMetricsRefresh(bool success, double elapsedSeconds)
    {
        _databaseSnapshotDuration.Observe(elapsedSeconds);
        if (success)
            _databaseSnapshotLastSuccess.SetToCurrentTimeUtc();
        else
            _databaseSnapshotFailures.Inc();
    }

    private static string GetUpdateType(Update update) => update.Type.ToString().ToLowerInvariant();

    private sealed class InFlightLease(Gauge.Child gauge) : IDisposable
    {
        public void Dispose() => gauge.Dec();
    }
}
