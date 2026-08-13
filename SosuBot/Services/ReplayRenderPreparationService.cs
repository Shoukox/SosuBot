using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using OsuParsers.Replays;
using SosuBot.Database.Database.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using Telegram.Bot;
using Telegram.Bot.Types;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.Services;

/// <summary>
/// Resolves render sources and turns them into a decoded, validated replay request.
/// It is shared by the regular replay command and the score-to-video command.
/// </summary>
public sealed class ReplayRenderPreparationService(
    BanchoApiV2 osuApi,
    ILogger<ReplayRenderPreparationService> logger)
{
    public async Task<ReplayRenderPreparationResult> PrepareMessageAsync(
        ITelegramBotClient botClient,
        Message message,
        RenderSettings userSettings,
        CancellationToken cancellationToken = default)
    {
        string[] parameters = (message.Text ?? string.Empty).GetCommandParameters() ?? [];
        bool autoRequested = parameters.Any(parameter =>
            parameter.Equals("auto", StringComparison.OrdinalIgnoreCase));
        string[] modParameters = parameters
            .Where(IsModParameter)
            .ToArray();
        List<string> sourceCandidates = parameters
            .Where(parameter =>
                !parameter.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                !IsModParameter(parameter))
            .ToList();
        sourceCandidates.AddRange(message.GetAllLinks());
        sourceCandidates.AddRange(GetMessageLinkCandidates(message.ReplyToMessage));

        MemoryStream? replayStream = null;
        BeatmapExtended? autoBeatmap = null;
        Score? score = null;
        long? scoreId = null;
        string? scoreLink = null;

        try
        {
            Document? document = message.ReplyToMessage?.Document ?? message.Document;
            if (document is not null)
            {
                replayStream = await DownloadTelegramDocumentAsync(botClient, document, cancellationToken);
            }
            else if (OsuHelper.ParseOsuScoreLink(sourceCandidates, out scoreId) is { } parsedScoreLink &&
                     scoreId is not null)
            {
                scoreLink = parsedScoreLink;
                score = await osuApi.Scores.GetScore(scoreId.Value);
                if (score is null)
                    return ReplayRenderPreparationResult.FailureResult(
                        ReplayRenderPreparationFailure.ScoreNotFound,
                        scoreLink);

                int? scoreBeatmapId = score.Beatmap?.Id ?? score.BeatmapId;
                if (autoRequested)
                {
                    autoBeatmap = await GetBeatmapAsync(scoreBeatmapId, cancellationToken);
                }
                else
                {
                    if (score.HasReplay != true)
                    {
                        return ReplayRenderPreparationResult.FailureResult(
                            ReplayRenderPreparationFailure.ScoreHasNoReplay,
                            scoreLink);
                    }

                    replayStream = await DownloadScoreReplayAsync(scoreId.Value, cancellationToken);
                }
            }
            else if (OsuHelper.ParseOsuBeatmapLink(
                         sourceCandidates,
                         out int? beatmapsetId,
                         out int? beatmapId) is not null)
            {
                // A beatmap link is an autoplay request even without the explicit
                // "auto" argument.
                autoRequested = true;
                beatmapId ??= await ResolveBeatmapIdFromSetAsync(beatmapsetId, cancellationToken);
                autoBeatmap = await GetBeatmapAsync(beatmapId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            replayStream?.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not resolve a replay render source from Telegram message");
            replayStream?.Dispose();
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.ApiUnavailable,
                scoreLink);
        }

        if (replayStream is null && autoBeatmap is null)
        {
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.Usage,
                scoreLink);
        }

        return await CompleteReplayPreparationAsync(
            replayStream,
            autoRequested,
            autoBeatmap,
            score,
            scoreId,
            scoreLink,
            modParameters,
            userSettings,
            cancellationToken);
    }

    public async Task<ReplayRenderPreparationResult> PrepareScoreAsync(
        long scoreId,
        string scoreLink,
        RenderSettings userSettings,
        CancellationToken cancellationToken = default)
    {
        Score? score;
        try
        {
            score = await osuApi.Scores.GetScore(scoreId);
            if (score is null)
            {
                return ReplayRenderPreparationResult.FailureResult(
                    ReplayRenderPreparationFailure.ScoreNotFound,
                    scoreLink);
            }

            if (score.HasReplay != true)
            {
                return ReplayRenderPreparationResult.FailureResult(
                    ReplayRenderPreparationFailure.ScoreHasNoReplay,
                    scoreLink);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not load score {ScoreId} for rendering", scoreId);
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.ApiUnavailable,
                scoreLink);
        }

        MemoryStream replayStream;
        try
        {
            replayStream = await DownloadScoreReplayAsync(scoreId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not download replay for score {ScoreId}", scoreId);
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.ApiUnavailable,
                scoreLink);
        }

        return await CompleteReplayPreparationAsync(
            replayStream,
            autoRequested: false,
            autoBeatmap: null,
            score,
            scoreId,
            scoreLink,
            modParameters: [],
            userSettings,
            cancellationToken);
    }

    public static RenderSettings CreateRenderSettings(
        RenderSettings userSettings,
        PreparedReplay replay)
    {
        bool useExperimentalRenderer = userSettings.UseExperimentalRenderer ||
                                        replay.IsRulesetNotStd ||
                                        replay.AutoRequested;
        return userSettings with
        {
            UseExperimentalRenderer = useExperimentalRenderer,
            UseAutoPlay = replay.AutoRequested,
            AutoBeatmapId = replay.AutoRequested ? replay.AutoBeatmap?.Id : null,
            AutoMods = replay.AutoRequested ? replay.AutoMods : []
        };
    }

    public static bool TryParseScoreLink(Message message, out long scoreId, out string scoreLink)
    {
        List<string> candidates = new(GetMessageLinkCandidates(message));
        candidates.AddRange(GetMessageLinkCandidates(message.ReplyToMessage));
        if (OsuHelper.ParseOsuScoreLink(candidates, out long? parsedScoreId) is { } parsedLink &&
            parsedScoreId is not null)
        {
            scoreId = parsedScoreId.Value;
            scoreLink = parsedLink;
            return true;
        }

        scoreId = default;
        scoreLink = string.Empty;
        return false;
    }

    private async Task<ReplayRenderPreparationResult> CompleteReplayPreparationAsync(
        MemoryStream? replayStream,
        bool autoRequested,
        BeatmapExtended? autoBeatmap,
        Score? score,
        long? scoreId,
        string? scoreLink,
        IReadOnlyCollection<string> modParameters,
        RenderSettings userSettings,
        CancellationToken cancellationToken)
    {
        Replay? replayInfo = null;
        if (replayStream is not null)
        {
            replayStream.Position = 0;
            try
            {
                replayInfo = OsuParsers.Decoders.ReplayDecoder.Decode(replayStream);
                replayStream.Position = 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "The replay file is invalid or corrupted");
                replayStream.Dispose();
                return ReplayRenderPreparationResult.FailureResult(
                    ReplayRenderPreparationFailure.InvalidReplay,
                    scoreLink);
            }

            if (autoRequested)
            {
                try
                {
                    LookupBeatmapResponse lookupResponse = await osuApi.Beatmaps.LookupBeatmap(
                        new() { Checksum = replayInfo.BeatmapMD5Hash });
                    autoBeatmap = lookupResponse.BeatmapExtended;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    replayStream.Dispose();
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not resolve the replay beatmap by checksum");
                    replayStream.Dispose();
                    return ReplayRenderPreparationResult.FailureResult(
                        ReplayRenderPreparationFailure.ApiUnavailable,
                        scoreLink);
                }
            }
        }

        if (autoRequested && (autoBeatmap?.Id is null || string.IsNullOrWhiteSpace(autoBeatmap.Checksum)))
        {
            replayStream?.Dispose();
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.BeatmapNotFound,
                scoreLink);
        }

        bool isRulesetNotStd = replayInfo is not null &&
                               replayInfo.Ruleset != OsuParsers.Enums.Ruleset.Standard;
        bool useExperimentalRenderer = userSettings.UseExperimentalRenderer ||
                                        isRulesetNotStd ||
                                        autoRequested;

        string[] autoMods = [];
        if (autoRequested)
        {
            try
            {
                Playmode playmode = GetPlaymode(autoBeatmap!);
                if (!TryParseAutoMods(modParameters, playmode, out autoMods))
                {
                    replayStream?.Dispose();
                    return ReplayRenderPreparationResult.FailureResult(
                        ReplayRenderPreparationFailure.Usage,
                        scoreLink);
                }
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Could not determine the autoplay beatmap ruleset");
                replayStream?.Dispose();
                return ReplayRenderPreparationResult.FailureResult(
                    ReplayRenderPreparationFailure.Usage,
                    scoreLink);
            }
        }

        int lengthInSeconds = autoRequested
            ? autoBeatmap!.TotalLength ?? 0
            : replayInfo?.ReplayFrames.Any() == true
                ? replayInfo.ReplayFrames.Max(frame => frame.Time) / 1000
                : 0;
        if (lengthInSeconds > (useExperimentalRenderer ? 20 : 30) * 60)
        {
            replayStream?.Dispose();
            return ReplayRenderPreparationResult.FailureResult(
                ReplayRenderPreparationFailure.BeatmapTooLong,
                scoreLink);
        }

        return ReplayRenderPreparationResult.Success(new PreparedReplay(
            replayStream,
            replayInfo,
            autoBeatmap,
            autoRequested,
            isRulesetNotStd,
            autoMods,
            score,
            scoreId,
            lengthInSeconds), scoreLink);
    }

    private async Task<MemoryStream> DownloadTelegramDocumentAsync(
        ITelegramBotClient botClient,
        Document document,
        CancellationToken cancellationToken)
    {
        TGFile telegramFile = await botClient.GetFile(document.FileId);
        MemoryStream replayStream = new();
        try
        {
            await botClient.DownloadFileConsideringLocalServer(telegramFile, replayStream);
            replayStream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();
            return replayStream;
        }
        catch
        {
            replayStream.Dispose();
            throw;
        }
    }

    private async Task<MemoryStream> DownloadScoreReplayAsync(
        long scoreId,
        CancellationToken cancellationToken)
    {
        await using Stream downloadedReplay = await osuApi.Scores.DownloadScoreReplay(scoreId);
        MemoryStream replayStream = new();
        try
        {
            await downloadedReplay.CopyToAsync(replayStream, cancellationToken);
            replayStream.Position = 0;
            return replayStream;
        }
        catch
        {
            replayStream.Dispose();
            throw;
        }
    }

    private async Task<BeatmapExtended?> GetBeatmapAsync(
        int? beatmapId,
        CancellationToken cancellationToken)
    {
        if (beatmapId is null)
            return null;

        GetBeatmapResponse? response = await osuApi.Beatmaps.GetBeatmap(beatmapId.Value, cancellationToken);
        return response?.BeatmapExtended;
    }

    private async Task<int?> ResolveBeatmapIdFromSetAsync(
        int? beatmapsetId,
        CancellationToken cancellationToken)
    {
        if (beatmapsetId is null)
            return null;

        BeatmapsetExtended? beatmapset = await osuApi.Beatmapsets.GetBeatmapset(beatmapsetId.Value,
            cancellationToken);
        return beatmapset?.Beatmaps?.FirstOrDefault(beatmap => beatmap.Id is not null)?.Id;
    }

    private static bool IsModParameter(string parameter) =>
        parameter.Length > 1 && parameter[0] == '+';

    private static IEnumerable<string> GetMessageLinkCandidates(Message? message)
    {
        if (message is null)
            return [];

        List<string> candidates = new(message.GetAllLinks());
        if (!string.IsNullOrWhiteSpace(message.Text))
            candidates.Add(message.Text);
        if (!string.IsNullOrWhiteSpace(message.Caption))
            candidates.Add(message.Caption);
        return candidates;
    }

    private static Playmode GetPlaymode(BeatmapExtended beatmap) =>
        beatmap.Mode?.ToLowerInvariant() switch
        {
            "osu" => Playmode.Osu,
            "taiko" => Playmode.Taiko,
            "fruits" or "catch" => Playmode.Catch,
            "mania" => Playmode.Mania,
            _ when beatmap.ModeInt is 0 => Playmode.Osu,
            _ when beatmap.ModeInt is 1 => Playmode.Taiko,
            _ when beatmap.ModeInt is 2 => Playmode.Catch,
            _ when beatmap.ModeInt is 3 => Playmode.Mania,
            _ => throw new InvalidOperationException("The beatmap mode is unknown.")
        };

    private static bool TryParseAutoMods(
        IEnumerable<string> modParameters,
        Playmode playmode,
        out string[] mods)
    {
        List<osu.Game.Rulesets.Mods.Mod> parsedMods = [];
        foreach (string parameter in modParameters)
        {
            if (!parameter.TryParseMods(playmode, out osu.Game.Rulesets.Mods.Mod[] parameterMods))
            {
                mods = [];
                return false;
            }

            parsedMods.AddRange(parameterMods);
        }

        mods = parsedMods
            .Where(mod => !mod.Acronym.Equals("NM", StringComparison.OrdinalIgnoreCase))
            .Select(mod => mod.Acronym.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return true;
    }
}

public enum ReplayRenderPreparationFailure
{
    None,
    Usage,
    ApiUnavailable,
    ScoreNotFound,
    ScoreHasNoReplay,
    InvalidReplay,
    BeatmapNotFound,
    BeatmapTooLong
}

public sealed record ReplayRenderPreparationResult(
    PreparedReplay? Request,
    ReplayRenderPreparationFailure Failure,
    string? ScoreLink)
{
    public static ReplayRenderPreparationResult Success(PreparedReplay request, string? scoreLink) =>
        new(request, ReplayRenderPreparationFailure.None, scoreLink);

    public static ReplayRenderPreparationResult FailureResult(
        ReplayRenderPreparationFailure failure,
        string? scoreLink) =>
        new(null, failure, scoreLink);
}

public sealed class PreparedReplay(
    MemoryStream? replayStream,
    Replay? replayInfo,
    BeatmapExtended? autoBeatmap,
    bool autoRequested,
    bool isRulesetNotStd,
    string[] autoMods,
    Score? score,
    long? scoreId,
    int lengthInSeconds) : IDisposable
{
    public MemoryStream? ReplayStream { get; } = replayStream;
    public Replay? ReplayInfo { get; } = replayInfo;
    public BeatmapExtended? AutoBeatmap { get; } = autoBeatmap;
    public bool AutoRequested { get; } = autoRequested;
    public bool IsRulesetNotStd { get; } = isRulesetNotStd;
    public string[] AutoMods { get; } = autoMods;
    public Score? Score { get; } = score;
    public long? ScoreId { get; } = scoreId;
    public int LengthInSeconds { get; } = lengthInSeconds;

    public void Dispose() => ReplayStream?.Dispose();
}
