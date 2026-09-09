using Microsoft.Extensions.DependencyInjection;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.PerformanceCalculator;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuCalcCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/calc"];
    public static readonly string Description = "[статистика или accuracy] [моды] рассчитать PP по карте";

    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;
    private BotContext _database = null!;
    private IPerformanceCalculator _performanceCalculator = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _performanceCalculator = Context.ServiceProvider.GetRequiredService<IPerformanceCalculator>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        TelegramChat? chat = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        await Task.Delay(500);

        string[] parameters = OsuCommandArguments.GetParameters(Context.Update.Text!);
        BeatmapExtended? beatmap = await GetBeatmapAsync(chat);
        if (beatmap is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }

        if (beatmap.ModeInt is not { } modeInt)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }
        Playmode playmode = (Playmode)modeInt;
        if (playmode is not (Playmode.Osu or Playmode.Taiko or Playmode.Catch or Playmode.Mania))
        {
            await waitMessage.EditAsync(Context.BotClient, LocalizationMessageHelper.CalcOnlySupportsModeMaps(language, "osu!std, osu!taiko, osu!catch, osu!mania"));
            return;
        }

        if (beatmap.CalculateObjectsAmount() >= 20000)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + $"\n{language.calc_tooManyObjects}");
            return;
        }

        if (!TryParseInput(parameters, playmode, beatmap, out double? accuracy, out Dictionary<HitResult, int>? statistics,
                out Mod[] mods))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + $"\n{language.calc_usage}");
            return;
        }

        using Stream beatmapFile = await _beatmapsService.DownloadOrCacheBeatmapAsync(beatmap.Id!.Value, Context.CancellationToken);
        Mod[] classicMods = AddClassicMod(mods, playmode);
        PPCalculationResult? lazer = await _performanceCalculator.CalculatePpAsync(
            beatmap.Id.Value, beatmapFile, accuracy, scoreMods: mods, scoreStatistics: statistics,
            rulesetId: (int)playmode, cancellationToken: Context.CancellationToken);
        beatmapFile.Position = 0;
        PPCalculationResult? stable = await _performanceCalculator.CalculatePpAsync(
            beatmap.Id.Value, beatmapFile, accuracy, scoreMods: classicMods, scoreStatistics: statistics,
            rulesetId: (int)playmode, cancellationToken: Context.CancellationToken);

        if (lazer is null || stable is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage);
            return;
        }

        Dictionary<HitResult, int> displayedStatistics = statistics ?? lazer.ScoreStatistics;
        string statsText = _scoreHelper.GetScoreStatisticsText(displayedStatistics, playmode);
        string textToSend = $"<b>{playmode.ToGamemode()}</b>\n<b>[{beatmap.Version.EncodeHtml()}]</b>\n\n" +
            $"<b>+{mods.ModsToString(playmode)}</b> {_scoreHelper.GetFormattedNumConsideringNull(stable.DifficultyAttributes.StarRating, round: false)}⭐️\n" +
            $"{statsText}/{displayedStatistics.GetValueOrDefault(HitResult.Miss, 0)}❌\n" +
            $"Lazer: {_scoreHelper.GetFormattedNumConsideringNull(lazer.CalculatedAccuracy * 100, round: false)}% - <b><u>{_scoreHelper.GetFormattedNumConsideringNull(lazer.PP)}pp</u></b>\n" +
            $"Stable: {_scoreHelper.GetFormattedNumConsideringNull(stable.CalculatedAccuracy * 100, round: false)}% - <b><u>{_scoreHelper.GetFormattedNumConsideringNull(stable.PP)}pp</u></b>";
        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }

    private async Task<BeatmapExtended?> GetBeatmapAsync(TelegramChat? chat)
    {
        var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId, out var beatmapId);
        if (link is not null && beatmapId is null && beatmapsetId is not null)
        {
            BeatmapsetExtended? set = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetId.Value, _osuApiV2);
            beatmapId = set?.Beatmaps?.FirstOrDefault()?.Id;
        }
        else if (link is null)
        {
            beatmapId = chat?.LastBeatmapId;
        }
        return beatmapId is null ? null : await _cachingHelper.GetOrCacheBeatmap(beatmapId.Value, _osuApiV2);
    }

    private static bool TryParseInput(string[] parameters, Playmode playmode, BeatmapExtended beatmap,
        out double? accuracy,
        out Dictionary<HitResult, int>? statistics, out Mod[] mods)
    {
        accuracy = null;
        statistics = null;
        mods = [];
        if (parameters.Length == 0)
            return false;

        if (TryParseAccuracy(parameters[0], out double parsedAccuracy))
        {
            if (parameters.Length > 2)
                return false;
            accuracy = parsedAccuracy;
            return parameters.Length != 2 || parameters[1].TryParseMods(playmode, out mods);
        }

        int statCount = playmode switch
        {
            Playmode.Osu => 3,
            Playmode.Taiko => 2,
            Playmode.Catch => 1,
            Playmode.Mania => 5,
            _ => 0
        };
        if (parameters.Length != statCount && parameters.Length != statCount + 1)
            return false;

        int[] values = new int[statCount];
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryParseCount(parameters[i], out values[i]) || values[i] < 0)
                return false;
        }
        if (parameters.Length == statCount + 1 && !parameters[^1].TryParseMods(playmode, out mods))
            return false;

        statistics = new Dictionary<HitResult, int>();
        int maximumTopJudgement = GetMaximumTopJudgement(beatmap, playmode);
        switch (playmode)
        {
            case Playmode.Osu:
                if (values[0] + values[1] + values[2] > maximumTopJudgement)
                    return false;
                statistics[HitResult.Great] = maximumTopJudgement - values[0] - values[1] - values[2];
                statistics[HitResult.Ok] = values[0];
                statistics[HitResult.Meh] = values[1];
                statistics[HitResult.Miss] = values[2];
                break;
            case Playmode.Taiko:
                if (values[0] + values[1] > maximumTopJudgement)
                    return false;
                statistics[HitResult.Great] = maximumTopJudgement - values[0] - values[1];
                statistics[HitResult.Ok] = values[0];
                statistics[HitResult.Meh] = 0;
                statistics[HitResult.Miss] = values[1];
                break;
            case Playmode.Catch:
                if (values[0] > maximumTopJudgement)
                    return false;
                statistics[HitResult.Great] = maximumTopJudgement - values[0];
                statistics[HitResult.LargeTickHit] = 0;
                statistics[HitResult.SmallTickHit] = 0;
                statistics[HitResult.SmallTickMiss] = 0;
                statistics[HitResult.Miss] = values[0];
                break;
            case Playmode.Mania:
                if (values.Sum() > maximumTopJudgement)
                    return false;
                statistics[HitResult.Perfect] = maximumTopJudgement - values.Sum();
                statistics[HitResult.Great] = values[0];
                statistics[HitResult.Good] = values[1];
                statistics[HitResult.Ok] = values[2];
                statistics[HitResult.Meh] = values[3];
                statistics[HitResult.Miss] = values[4];
                break;
        }
        return true;
    }

    private static int GetMaximumTopJudgement(BeatmapExtended beatmap, Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => beatmap.CountCircles!.Value + beatmap.CountSliders!.Value + beatmap.CountSpinners!.Value,
            Playmode.Mania => beatmap.CountCircles!.Value + 2 * beatmap.CountSliders!.Value + beatmap.CountSpinners!.Value,
            Playmode.Taiko or Playmode.Catch => beatmap.MaxCombo ??
                (beatmap.CountCircles!.Value + beatmap.CountSliders!.Value + beatmap.CountSpinners!.Value),
            _ => 0
        };
    }

    private static bool TryParseAccuracy(string text, out double accuracy)
    {
        string normalized = text.Trim().TrimEnd('%');
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double percentage) ||
            (!text.EndsWith('%') && !text.Contains('.')))
        {
            accuracy = 0;
            return false;
        }
        accuracy = text.EndsWith('%') || percentage > 1 ? percentage / 100 : percentage;
        return accuracy is >= 0 and <= 1;
    }

    private static bool TryParseCount(string text, out int count)
    {
        return int.TryParse(text.Trim().TrimStart('x', 'X'), out count);
    }

    private static Mod[] AddClassicMod(Mod[] mods, Playmode playmode)
    {
        Mod classic = playmode switch
        {
            Playmode.Osu => new OsuModClassic(),
            Playmode.Taiko => new TaikoModClassic(),
            Playmode.Catch => new CatchModClassic(),
            Playmode.Mania => new ManiaModClassic(),
            _ => throw new ArgumentOutOfRangeException(nameof(playmode))
        };
        return mods.Concat([classic]).Distinct().ToArray();
    }
}
