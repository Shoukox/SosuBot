using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuCalcManiaCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/calculatemania", "/calcmania"];
    private ILogger<PPCalculator> _loggerPpCalculator = null!;
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _loggerPpCalculator = Context.ServiceProvider.GetRequiredService<ILogger<PPCalculator>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        if (parameters.Length <= 4 || parameters.Length >= 7)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        BeatmapsetExtended? beatmapset = null;

        var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId, out var beatmapId);
        if (link is not null)
        {
            // calc x300 x200 x100 x50 xMiss, with reply
            if (beatmapId is null && beatmapsetId is not null)
            {
                beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                beatmapId = beatmapset.Beatmaps![0].Id;
            }
        }
        else
        {
            // calc x300 x200 x100 x50 xMiss, no reply
            beatmapId = chatInDatabase!.LastBeatmapId;
        }

        if(beatmapId is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }

        var getBeatmapResponse = await _osuApiV2.Beatmaps.GetBeatmap(beatmapId.Value);
        if (getBeatmapResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_beatmapNotFound);
            return;
        }

        var beatmap = getBeatmapResponse.BeatmapExtended!;
        if (beatmap.ModeInt != (int)Playmode.Mania)
        {
            await waitMessage.EditAsync(Context.BotClient, $"Эта команда поддерживает только {Playmode.Mania.ToGamemode()} карты");
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
        if (parameters.Length == 6)
        {
            modsFromMessage = parameters[5].ToMods(playmode).Except([new ManiaModClassic()]).ToArray();
        }

        // get score statistics from parameters
        Dictionary<HitResult, int> scoreStatistics = beatmap.GetMaximumStatistics(playmode);
        if(!int.TryParse(parameters[0], out int greatCount) 
            || !int.TryParse(parameters[1], out int goodCount) 
            || !int.TryParse(parameters[1], out int okCount) 
            || !int.TryParse(parameters[1], out int mehCount) 
            || !int.TryParse(parameters[2], out int missCount))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\n/calc x300 x200 x100 x50 xMiss [mods]\nПервые параметры - цифры. Моды (HDDT) - опциональны");
            return;
        }

        if(greatCount < 0 || goodCount < 0 || okCount < 0 || mehCount < 0 || missCount < 0 || greatCount + goodCount + okCount + mehCount + missCount > scoreStatistics[HitResult.Perfect])
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\nНекорректная статистика скора");
            return;
        }
        
        scoreStatistics[HitResult.Great] = greatCount;
        scoreStatistics[HitResult.Good] = goodCount;
        scoreStatistics[HitResult.Ok] = okCount;
        scoreStatistics[HitResult.Meh] = mehCount;
        scoreStatistics[HitResult.Miss] = missCount;
        scoreStatistics[HitResult.Perfect] = scoreStatistics[HitResult.Perfect] - scoreStatistics[HitResult.Great] - scoreStatistics[HitResult.Good] - scoreStatistics[HitResult.Ok] - scoreStatistics[HitResult.Meh] - scoreStatistics[HitResult.Miss];

        var ppCalculator = new PPCalculator(_loggerPpCalculator);
        var ppLazer = await ppCalculator.CalculatePpAsync(beatmap.Id!.Value, null,
                             scoreMaxCombo: beatmap.MaxCombo,
                             passed: true,
                             scoreMods: modsFromMessage,
                             scoreStatistics: scoreStatistics,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken);

        var ppClassic = await ppCalculator.CalculatePpAsync(beatmap.Id!.Value, null,
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

        double? difficultyRatingForGivenMods = ppCalculator.LastDifficultyAttributes?.StarRating;
        int miss = scoreStatistics.GetValueOrDefault(HitResult.Miss, 0);

        string textToSend = $"<b>{gamemode}</b>\n" +
            $"<b>[{beatmap.Version.EncodeHtml()}]</b>\n\n" +
            $"<b>+{modsFromMessage.ModsToString(playmode)}</b> {ScoreHelper.GetFormattedNumConsideringNull(difficultyRatingForGivenMods, round: false)}⭐️\n" +
            $"{ScoreHelper.GetScoreStatisticsText(scoreStatistics, playmode)}/{miss}❌\n" +
            $"Lazer: {ScoreHelper.GetFormattedNumConsideringNull(ppLazer.CalculatedAccuracy * 100, round: false)}% - <b><u>{ScoreHelper.GetFormattedNumConsideringNull(ppLazer.Pp)}pp</u></b>\n" +
            $"Stable: {ScoreHelper.GetFormattedNumConsideringNull(ppClassic.CalculatedAccuracy * 100, round: false)}% - <b><u>{ScoreHelper.GetFormattedNumConsideringNull(ppClassic.Pp)}pp</u></b>";
        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}