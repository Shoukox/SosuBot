using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.PerformanceCalculator.Models;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Text;

public sealed class TextHandler : CommandBase<Message>
{
    private ILogger<TextHandler> _logger = null!;
    private ILogger<PPCalculator> _loggerPpCalculator = null!;
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<TextHandler>>();
        _loggerPpCalculator = Context.ServiceProvider.GetRequiredService<ILogger<PPCalculator>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        ILocalization language = new Russian();

        await HandleBeatmapLink(language);
        await HandleUserProfileLink(language);
    }

    private async Task HandleUserProfileLink(ILocalization language)
    {
        var userProfileLink = OsuHelper.ParseOsuUserLink(Context.Update.GetAllLinks(), out var userId);
        if (userProfileLink == null) return;
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        var user = (await _osuApiV2.Users.GetUser($"{userId}", new GetUserQueryParameters()))!.UserExtend!;

        var playmode = user.Playmode!.ParseRulesetToPlaymode();
        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(Context.Database, user, playmode, currentPp);

        DateTime.TryParse(user.JoinDate?.Value, out var registerDateTime);
        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{UserHelper.CountryCodeToFlag(user.CountryCode ?? "nn")}",
            $"{ScoreHelper.GetFormattedNumConsideringNull(currentPp)}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
            $"{registerDateTime:dd.MM.yyyy HH:mm:ss}",
            $"{user.UserAchievements?.Length ?? 0}",
            $"{OsuConstants.TotalAchievementsCount}",
            $"{user.Statistics.GradeCounts!.SSH}",
            $"{user.Statistics.GradeCounts!.SH}",
            $"{user.Statistics.GradeCounts!.SS}",
            $"{user.Statistics.GradeCounts!.S}",
            $"{user.Statistics.GradeCounts!.A}"
        ]);
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
        {
            [
                new InlineKeyboardButton("Standard")
                    { CallbackData = $"{Context.Update.Chat.Id} user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko")
                    { CallbackData = $"{Context.Update.Chat.Id} user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch")
                    { CallbackData = $"{Context.Update.Chat.Id} user 2 {user.Username}" },
                new InlineKeyboardButton("Mania")
                    { CallbackData = $"{Context.Update.Chat.Id} user 3 {user.Username}" }
            ]
        });

        await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }

    private async Task HandleBeatmapLink(ILocalization language)
    {
        var beatmapLink = OsuHelper.ParseOsuBeatmapLink(Context.Update.GetAllLinks(), out var beatmapsetId,
            out var beatmapId);
        if (beatmapLink == null) return;
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        BeatmapsetExtended? beatmapset = null;
        BeatmapExtended? beatmap = null;
        if (beatmapId is null && beatmapsetId is not null)
        {
            beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
            beatmapId = beatmapset.Beatmaps!.OrderByDescending(m => m.DifficultyRating).First().Id;
        }

        beatmap ??= (await _osuApiV2.Beatmaps.GetBeatmap(beatmapId!.Value))!.BeatmapExtended!;

        var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
        bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;

        beatmapset ??= await _osuApiV2.Beatmapsets.GetBeatmapset(beatmap.BeatmapsetId.Value);

        var playmode = beatmap.Mode!.ParseRulesetToPlaymode();
        var classicMod = OsuHelper.GetClassicMode(playmode);
        var modsFromMessage = beatmapLink.Contains('+')
            ? beatmapLink.Split('+')[1].ToMods(playmode).Except([classicMod]).ToArray()
            : [];

        var classicModsToApply = modsFromMessage.Concat([classicMod]).Distinct().ToArray();
        var lazerModsToApply = modsFromMessage.Distinct().ToArray();

        var ppCalculator = new PPCalculator(_loggerPpCalculator);
        var calculatedPp = new {
            ClassicSS = (PPCalculationResult?)null,
            Classic99 = (PPCalculationResult?)null,
            Classic98 = (PPCalculationResult?)null,
            LazerSS = (PPCalculationResult?)null,
            Lazer99 = (PPCalculationResult?)null,
            Lazer98 = (PPCalculationResult?)null,
        };
        if (!beatmapContainsTooManyHitObjects)
        {
            calculatedPp = new 
            {
                ClassicSS = (PPCalculationResult?)await ppCalculator.CalculatePpAsync(
              accuracy: 1,
              beatmapId: beatmap.Id.Value,
              scoreMaxCombo: null,
              scoreMods: classicModsToApply,
              scoreStatistics: null,
              rulesetId: (int)playmode,
              cancellationToken: Context.CancellationToken),

                Classic99 = playmode == Playmode.Mania
              ? null
              : await ppCalculator.CalculatePpAsync(
                  accuracy: 0.99,
                  beatmapId: beatmap.Id.Value,
                  scoreMaxCombo: null,
                  scoreMods: classicModsToApply,
                  scoreStatistics: null,
                  rulesetId: (int)playmode,
                  cancellationToken: Context.CancellationToken),

                Classic98 = playmode == Playmode.Mania
              ? null
              : await ppCalculator.CalculatePpAsync(
                  accuracy: 0.98,
                  beatmapId: beatmap.Id.Value,
                  scoreMaxCombo: null,
                  scoreMods: classicModsToApply,
                  scoreStatistics: null,
                  rulesetId: (int)playmode,
                  cancellationToken: Context.CancellationToken),

                LazerSS = (PPCalculationResult?)await ppCalculator.CalculatePpAsync(
              accuracy: 1,
              beatmapId: beatmap.Id.Value,
              scoreMaxCombo: null,
              scoreMods: lazerModsToApply,
              scoreStatistics: null,
              rulesetId: (int)playmode,
              cancellationToken: Context.CancellationToken),

                Lazer99 = playmode == Playmode.Mania
              ? null
              : await ppCalculator.CalculatePpAsync(
                  accuracy: 0.99,
                  beatmapId: beatmap.Id.Value,
                  scoreMaxCombo: null,
                  scoreMods: lazerModsToApply,
                  scoreStatistics: null,
                  rulesetId: (int)playmode,
                  cancellationToken: Context.CancellationToken),

                Lazer98 = playmode == Playmode.Mania
              ? null
              : await ppCalculator.CalculatePpAsync(
                  accuracy: 0.98,
                  beatmapId: beatmap.Id.Value,
                  scoreMaxCombo: null,
                  scoreMods: lazerModsToApply,
                  scoreStatistics: null,
                  rulesetId: (int)playmode,
                  cancellationToken: Context.CancellationToken)
            };
        }
      

        var duration = $"{beatmap.TotalLength / 60}m{beatmap.TotalLength % 60:00}s";
        var padLength = 9;

        string classicSSText = $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}")}%".PadRight(padLength) + "| " +
                               $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.Pp)}pp\n";
        string classic99Text = playmode == Playmode.Mania
            ? ""
            : $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}")}%".PadRight(padLength) + "| " +
              $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.Pp)}pp\n";
        string classic98Text = playmode == Playmode.Mania
            ? ""
            : $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}")}%".PadRight(padLength) + "| " +
              $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.Pp)}pp\n";
        string lazerSSText = $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}")}%".PadRight(padLength) + "| " +
                             $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.Pp)}pp\n";
        string lazer99Text = playmode == Playmode.Mania
            ? ""
            : $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}")}%".PadRight(padLength) + "| " +
              $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.Pp)}pp\n";
        string lazer98Text = playmode == Playmode.Mania
            ? ""
            : $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}")}%".PadRight(padLength) + "| " +
              $"{ScoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.Pp)}pp\n";

        // Calculate diff rating
        double? difficultyRatingForGivenMods = ppCalculator.LastDifficultyAttributes?.StarRating;
        if (difficultyRatingForGivenMods == null)
        {
            var beatmapAttributesResponse = await _osuApiV2.Beatmaps.GetBeatmapAttributes(beatmap.Id.Value, new() { RulesetId = ((int)playmode).ToString(), Mods = lazerModsToApply.Select(m => new Mod { Acronym = m.Acronym}).ToArray() });
            difficultyRatingForGivenMods = beatmapAttributesResponse?.DifficultyAttributes?.StarRating;
        }

        var textToSend = language.send_mapInfo.Fill([
            $"{playmode.ToGamemode()}",
            $"{beatmap.Version.EncodeHtml()}",
            $"{beatmap.DifficultyRating:N2}",
            $"{duration}",
            $"{beatmapset.Creator}",
            $"{beatmap.Status}",
            $"{beatmap.Id}",
            $"{beatmap.CS}",
            $"{beatmap.AR}",
            $"{beatmap.Drain}",
            $"{beatmap.BPM}",
            $"{lazerModsToApply.ModsToString(playmode)}",

            $"{ScoreHelper.GetFormattedNumConsideringNull(difficultyRatingForGivenMods)}",

            classicSSText,
            classic99Text,
            classic98Text,
            lazerSSText,
            lazer99Text,
            lazer98Text,
        ]);

        var photo = new InputFileUrl(
            new Uri($"https://assets.ppy.sh/beatmaps/{beatmapset.Id}/covers/card@2x.jpg"));
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song preview")
        { CallbackData = $"{Context.Update.Chat.Id} songpreview {beatmapset.Id}" });

        if (beatmapContainsTooManyHitObjects)
            textToSend += "\nВ карте слишком много объектов, пп расчет не будет проведен.";

        try
        {
            await Context.Update.ReplyPhotoAsync(Context.BotClient, photo, textToSend,
                replyMarkup: ik);
        }
        catch
        {
            await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }

        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        chatInDatabase!.LastBeatmapId = beatmapId;
    }
}