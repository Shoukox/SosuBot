using osu.Game.Rulesets.Mods;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Text;

public class TextHandler : CommandBase<Message>
{
    public override async Task ExecuteAsync()
    {
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

        var user = (await Context.OsuApiV2.Users.GetUser($"{userId}", new GetUserQueryParameters()))!.UserExtend!;

        var playmode = user.Playmode!.ParseRulesetToPlaymode();
        double? savedPPInDatabase = null;
        double? currentPP = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPPDifferenceTextAsync(Context.Database, user, playmode, currentPP,
                savedPPInDatabase);

        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{user.Statistics.GlobalRank}",
            $"{user.Statistics.CountryRank}",
            $"{user.CountryCode}",
            $"{currentPP:N2}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
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
            beatmapset = await Context.OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
            beatmapId = beatmapset.Beatmaps!.OrderByDescending(m => m.DifficultyRating).First().Id;
            ;
        }

        beatmap ??= (await Context.OsuApiV2.Beatmaps.GetBeatmap(beatmapId!.Value))!.BeatmapExtended!;
        var sum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
        if (sum is > 20_000) return;

        beatmapset ??= await Context.OsuApiV2.Beatmapsets.GetBeatmapset(beatmap!.BeatmapsetId.Value);

        var playmode = beatmap.Mode!.ParseRulesetToPlaymode();
        var classicMod = OsuHelper.GetClassicMode(playmode);
        Mod[] modsFromMessage;
        if (beatmapLink.Contains('+'))
            modsFromMessage = beatmapLink.Split('+')[1].ToMods(playmode);
        else
            modsFromMessage = [];

        var classicModsToApply = modsFromMessage.Concat([classicMod]).Distinct().ToArray();
        var lazerModsToApply = modsFromMessage.Distinct().ToArray();

        var ppCalculator = new PPCalculator();
        var calculatedPP = new
        {
            ClassicSS = await ppCalculator.CalculatePPAsync(
                accuracy: 1,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: classicModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken),

            Classic99 = await ppCalculator.CalculatePPAsync(
                accuracy: 0.99,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: classicModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken),

            Classic98 = await ppCalculator.CalculatePPAsync(
                accuracy: 0.98,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: classicModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken),

            LazerSS = await ppCalculator.CalculatePPAsync(
                accuracy: 1,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: lazerModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken),

            Lazer99 = await ppCalculator.CalculatePPAsync(
                accuracy: 0.99,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: lazerModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken),

            Lazer98 = await ppCalculator.CalculatePPAsync(
                accuracy: 0.98,
                beatmapId: beatmap.Id.Value,
                scoreMaxCombo: null,
                scoreMods: lazerModsToApply,
                scoreStatistics: null,
                rulesetId: (int)playmode,
                cancellationToken: Context.CancellationToken)
        };

        var duration = $"{beatmap.TotalLength / 60}:{beatmap.TotalLength % 60:00}";
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
            $"{ppCalculator.LastDifficultyAttributes.StarRating:N2}",
            $"{calculatedPP.ClassicSS:N0}",
            $"{calculatedPP.LazerSS:N0}",

            $"{calculatedPP.Classic99:N0}",
            $"{calculatedPP.Lazer99:N0}",

            $"{calculatedPP.Classic98:N0}",
            $"{calculatedPP.Lazer98:N0}"
        ]);

        var photo = new InputFileUrl(
            new Uri($"https://assets.ppy.sh/beatmaps/{beatmapset.Id}/covers/card@2x.jpg"));
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song preview")
            { CallbackData = $"{Context.Update.Chat.Id} songpreview {beatmapset.Id}" });

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