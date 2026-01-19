using Microsoft.Extensions.DependencyInjection;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Synchronization;
using Telegram.Bot.Types;
using SosuBot.TelegramHandlers.Abstract;
using SosuBot.Services;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuCalcCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/calculate", "/calcstd", "/calcosu", "/calc"];
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();
        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        if (parameters.Length <= 2 || parameters.Length >= 5)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        BeatmapsetExtended? beatmapset = null;

        var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId, out var beatmapId);
        if (link is not null)
        {
            // calc x100 x50 xMiss, with reply
            if (beatmapId is null && beatmapsetId is not null)
            {
                beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetId.Value, _osuApiV2);
                beatmapId = beatmapset!.Beatmaps![0].Id;
            }
        }
        else
        {
            // calc x100 x50 xMiss, no reply
            beatmapId = chatInDatabase!.LastBeatmapId;
        }

        if (beatmapId is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }

        var getBeatmapResponse = await _cachingHelper.GetOrCacheBeatmap(beatmapId.Value, _osuApiV2);
        if (getBeatmapResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }

        var beatmap = getBeatmapResponse;
        if (beatmap.ModeInt != (int)Playmode.Osu)
        {
            await waitMessage.EditAsync(Context.BotClient, $"Эта команда поддерживает только {Playmode.Osu.ToGamemode()} карты");
            return;
        }
        var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
        bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;
        if (beatmapContainsTooManyHitObjects)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\nВ карте слишком много объектов.");
            return;
        }

        Playmode playmode = (Playmode)beatmap.ModeInt;
        string gamemode = playmode.ToGamemode();

        // get mods from parameters
        osu.Game.Rulesets.Mods.Mod[] modsFromMessage = [];
        if (parameters.Length == 4)
        {
            modsFromMessage = parameters[3].ToMods(playmode).Except([new OsuModClassic()]).ToArray();
        }

        // get score statistics from parameters
        Dictionary<HitResult, int> scoreStatistics = beatmap.GetMaximumStatistics();
        if (!int.TryParse(parameters[0], out int okCount) || !int.TryParse(parameters[1], out int mehCount) || !int.TryParse(parameters[2], out int missCount))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\n/calc x100 x50 xMiss [mods]\nПервые три параметра - цифры. Моды (HDDT) - опциональны");
            return;
        }

        if (okCount < 0 || mehCount < 0 || missCount < 0 || okCount + mehCount + missCount > scoreStatistics[HitResult.Great])
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\nНекорректная статистика скора");
            return;
        }

        scoreStatistics[HitResult.Ok] = okCount;
        scoreStatistics[HitResult.Meh] = mehCount;
        scoreStatistics[HitResult.Miss] = missCount;
        scoreStatistics[HitResult.Great] = scoreStatistics[HitResult.Great] - scoreStatistics[HitResult.Ok] - scoreStatistics[HitResult.Meh] - scoreStatistics[HitResult.Miss];

        var ppCalculator = new PPCalculator();
        var beatmapFile = await _beatmapsService.DownloadOrCacheBeatmap(beatmap.Id!.Value);
        var ppLazer = await ppCalculator.CalculatePpAsync(
                             beatmapId: beatmap.Id!.Value,
                             beatmapFile: beatmapFile,
                             accuracy: null,
                             scoreMaxCombo: beatmap.MaxCombo,
                             passed: true,
                             scoreMods: modsFromMessage,
                             scoreStatistics: scoreStatistics,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken);

        var ppClassic = await ppCalculator.CalculatePpAsync(
                             beatmapId: beatmap.Id!.Value,
                             beatmapFile: beatmapFile,
                             accuracy: null,
                             scoreMaxCombo: beatmap.MaxCombo,
                             passed: true,
                             scoreMods: modsFromMessage.Append(new OsuModClassic()).ToArray(),
                             scoreStatistics: scoreStatistics,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken);

        if (ppLazer is null || ppClassic is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage);
            return;
        }

        double? difficultyRatingForGivenMods = ppClassic.DifficultyAttributes.StarRating;
        int miss = scoreStatistics.GetValueOrDefault(HitResult.Miss, 0);

        string textToSend = $"<b>{gamemode}</b>\n" +
            $"<b>[{beatmap.Version.EncodeHtml()}]</b>\n\n" +
            $"<b>+{modsFromMessage.ModsToString(playmode)}</b> {_scoreHelper.GetFormattedNumConsideringNull(difficultyRatingForGivenMods, round: false)}⭐️\n" +
            $"{_scoreHelper.GetScoreStatisticsText(scoreStatistics, playmode)}/{miss}❌\n" +
            $"Lazer: {_scoreHelper.GetFormattedNumConsideringNull(ppLazer.CalculatedAccuracy * 100, round: false)}% - <b><u>{_scoreHelper.GetFormattedNumConsideringNull(ppLazer.Pp)}pp</u></b>\n" +
            $"Stable: {_scoreHelper.GetFormattedNumConsideringNull(ppClassic.CalculatedAccuracy * 100, round: false)}% - <b><u>{_scoreHelper.GetFormattedNumConsideringNull(ppClassic.Pp)}pp</u></b>";
        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}