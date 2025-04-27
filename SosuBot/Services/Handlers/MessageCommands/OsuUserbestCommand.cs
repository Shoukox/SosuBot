using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2;
using OsuApi.Core.V2.Beatmaps.Models.HttpIO;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using OsuApi.Core.V2.Users.Models.HttpIO;
using Sosu.Localization;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class OsuUserbestCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/userbest", "/best"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);

            Score[]? bestScores = null;
            string osuUsernameForUserbest = string.Empty;
            string ruleset = Ruleset.Osu;

            string[] parameters = Context.Text!.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser);
                    return;
                }
                ruleset = osuUserInDatabase.OsuMode;
                osuUsernameForUserbest = osuUserInDatabase.OsuUsername;
                bestScores = (await OsuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Limit = 5, Mode = ruleset }))!.Scores;
            }
            else
            {
                if (parameters.Length == 2)
                {
                    ruleset = parameters[2].ParseToRuleset()!;
                }

                var userResponse = await OsuApiV2.Users.GetUser(parameters[0], new());
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser + "\n\n" + language.error_hintReplaceSpaces);
                    return;
                }

                ruleset = userResponse.UserExtend!.Playmode!;
                osuUsernameForUserbest = userResponse.UserExtend!.Username!;
                var userbestResponse = await OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value, ScoreType.Best, new() { Limit = 5, Mode = ruleset });
                bestScores = userbestResponse!.Scores;
            }

            if (bestScores.Length == 0)
            {
                await waitMessage.EditAsync(BotClient, language.error_noBestScores);
                return;
            }

            string gamemode = ruleset.ParseFromRuleset()!;
            string textToSend = $"{osuUsernameForUserbest} (<b>{gamemode}</b>)\n\n";

            GetBeatmapResponse[] beatmaps = bestScores.Select(async score => await OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result).ToArray()!;
            for (int i = 0; i <= bestScores.Length - 1; i++)
            {
                var score = bestScores[i];
                var beatmap = beatmaps[i];
                textToSend += language.command_userbest.Fill([$"{i + 1}", $"{score.Rank}", $"{score.BeatmapId}", $"{score.Beatmapset!.Title}", $"{score.Beatmap!.Version}", $"{score.Beatmapset.Status}", $"{score.Statistics!.Great}", $"{score.Statistics!.Ok}", $"{score.Statistics!.Meh}", $"{score.Statistics!.Miss}", $"{score.Accuracy * 100:N2}", $"+{string.Join("", score.Mods!.Select(m => m.Acronym))}", $"{score.MaxCombo}", $"{beatmap.BeatmapExtended!.MaxCombo}", $"{score.Pp:N2}"]);
            }

            var ik = new InlineKeyboardMarkup(
                new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{chatInDatabase!.ChatId} userbest previous 0 {gamemode} {osuUsernameForUserbest}" }, new InlineKeyboardButton("Next") { CallbackData = $"{chatInDatabase.ChatId} userbest next 0 {gamemode} {osuUsernameForUserbest}" } }
            );
            await waitMessage.EditAsync(BotClient, textToSend, replyMarkup: ik);
        }
    }
}
