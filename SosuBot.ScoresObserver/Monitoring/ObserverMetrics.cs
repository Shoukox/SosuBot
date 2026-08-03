using Prometheus;

namespace SosuBot.ScoresObserver.Monitoring;

public sealed class ObserverMetrics
{
    private readonly Counter _polls = Metrics.CreateCounter(
        "sosubot_scores_observer_polls_total",
        "Number of ScoresObserver polling operations.",
        ["source", "status"]);

    private readonly Counter _trackedScores = Metrics.CreateCounter(
        "sosubot_scores_observer_tracked_scores_total",
        "Number of newly detected tracked-player scores persisted to PostgreSQL.");

    private readonly Counter _deliveries = Metrics.CreateCounter(
        "sosubot_scores_observer_deliveries_created_total",
        "Number of Telegram delivery records created for tracked scores.");

    private readonly Gauge _observedPlayers = Metrics.CreateGauge(
        "sosubot_scores_observer_observed_players",
        "Number of players in the current observer polling cycle.");

    private readonly Gauge _lastSuccessfulPoll = Metrics.CreateGauge(
        "sosubot_scores_observer_last_success_unixtime",
        "Unix timestamp of the last successful ScoresObserver poll.",
        ["source"]);

    private readonly Gauge _leader = Metrics.CreateGauge(
        "sosubot_scores_observer_is_leader",
        "Whether this ScoresObserver process currently owns the PostgreSQL leader lock (1 = leader).");

    public void RecordPoll(string source, bool success)
    {
        _polls.WithLabels(source, success ? "success" : "error").Inc();
        if (success)
            _lastSuccessfulPoll.WithLabels(source).Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public void RecordTrackedScores(int scoreCount, int deliveryCount)
    {
        if (scoreCount > 0) _trackedScores.Inc(scoreCount);
        if (deliveryCount > 0) _deliveries.Inc(deliveryCount);
    }

    public void SetObservedPlayers(int count) => _observedPlayers.Set(count);

    public void SetLeader(bool isLeader) => _leader.Set(isLeader ? 1 : 0);
}
