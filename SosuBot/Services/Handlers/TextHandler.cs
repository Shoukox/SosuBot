using osu.Game.Rulesets.Osu.Mods;
using OsuApi.Core.V2.Users.Models;
using PerfomanceCalculator;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
using SosuBot.Services.Handlers.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Text
{
    public class TextHandler : CommandBase<Message>
    {
        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            string text = Context.Text!;

            string? userProfileLink = OsuHelper.ParseOsuUserLink(text, out int? userId);
            string? beatmapLink = OsuHelper.ParseOsuBeatmapLink(text, out int? beatmapsetId, out int? beatmapId);

            if (userProfileLink is not null)
            {
                UserExtend user = (await OsuApiV2.Users.GetUser($"{userId}", new()))!.UserExtend!;

                Playmode playmode = user.Playmode!.ParseRulesetToPlaymode();
                double? savedPPInDatabase = null;
                double? currentPP = user.Statistics!.Pp;
                string ppDifferenceText = await UserHelper.GetPPDifferenceTextAsync(Database, user, playmode, currentPP, savedPPInDatabase);

                string textToSend = language.command_user.Fill([
                    $"{playmode.ToGamemode()}",
                $"{user.GetProfileUrl()}",
                $"{user.Username}",
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
                $"{user.Statistics.GradeCounts!.A}"]);
                var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
                {
                [new InlineKeyboardButton("Standard") {CallbackData = $"{Context.Chat.Id} user 0 {user.Username}"}, new InlineKeyboardButton("Taiko") { CallbackData = $"{Context.Chat.Id} user 1 {user.Username}" }],
                [new InlineKeyboardButton("Catch") {CallbackData = $"{Context.Chat.Id} user 2 {user.Username}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{Context.Chat.Id} user 3 {user.Username}" }]
                });

                await Context.ReplyAsync(BotClient, textToSend, replyMarkup: ik);
            }
            if (beatmapLink is not null)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    BeatmapsetExtended beatmapsetToGetBeatmapId = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                    beatmapId = beatmapsetToGetBeatmapId.Beatmaps!.OrderByDescending(m => m.DifficultyRating).First().Id!;
                }

                var beatmap = (await OsuApiV2.Beatmaps.GetBeatmap(beatmapId!.Value))!.BeatmapExtended;
                var beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmap!.BeatmapsetId.Value);

                Playmode playmode = beatmap.Mode!.ParseRulesetToPlaymode();
                var maximumStatistics = beatmap.GetMaximumStatistics();
                var ppCalculator = new PPCalculator();
                var calculatedPP = new
                {
                    ClassicSS = await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        beatmap.MaxCombo!.Value,
                        [new OsuModClassic()],
                        statistics: maximumStatistics,
                        maxStatistics: maximumStatistics,
                        rulesetId: (int)playmode),
                    LazerSS = await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        beatmap.MaxCombo!.Value,
                        [],
                        statistics: maximumStatistics,
                        maxStatistics: maximumStatistics,
                        rulesetId: (int)playmode),
                };

                string duration = $"{beatmap.HitLength / 60}:{(beatmap.HitLength % 60):00}";
                string textToSend = language.send_mapInfo.Fill([
                    $"{beatmap.Version}",
                    $"{beatmap.DifficultyRating:N2}",
                    $"{duration}",
                    $"{beatmapset.Creator}",
                    $"{beatmap.Status}",
                    $"{beatmap.CS}",
                    $"{beatmap.AR}",
                    $"{beatmap.Drain}",
                    $"{beatmap.BPM}",
                    $"{calculatedPP.LazerSS:N0}",
                    $"{calculatedPP.ClassicSS:N0}"]);

                var photo = new InputFileUrl(new Uri($"https://assets.ppy.sh/beatmaps/{beatmapset.Id}/covers/card@2x.jpg"));
                var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song preview") { CallbackData = $"{Context.Chat.Id} songpreview {beatmapset.Id}" });

                try
                {
                    await BotClient.SendPhoto(Context.Chat.Id, photo, caption: textToSend, ParseMode.Html, replyMarkup: ik);
                }
                catch
                {
                    await BotClient.SendMessage(Context.Chat.Id, textToSend, ParseMode.Html, replyMarkup: ik, linkPreviewOptions: true);
                }

                TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
                chatInDatabase!.LastBeatmapId = beatmapId;
            }
        }
    }
}
