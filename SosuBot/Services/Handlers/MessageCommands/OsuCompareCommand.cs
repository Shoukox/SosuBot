using OsuApi.Core.V2.Scores.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class OsuCompareCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/compare", "/cmp"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);
            List<OsuUser> foundChatMembers = new List<OsuUser>();

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);
            string[] parameters = Context.Text!.GetCommandParameters()!;
            
            if (parameters.Length < 2)
            {
                await waitMessage.EditAsync(BotClient, language.error_argsLength);
                return;
            }

            string? ruleset = parameters[2].ParseToRuleset();
            if (ruleset is null)
            {
                await Context.ReplyAsync(BotClient, language.error_modeIncorrect);
                return;
            }

            string gamemode = parameters.Length == 2 ? Ruleset.Osu : ruleset;
            var getUser1Response = await OsuApiV2.Users.GetUser($"@{parameters[0]}", new(), mode: gamemode);
            var getUser2Response = await OsuApiV2.Users.GetUser($"@{parameters[1]}", new(), mode: gamemode);

            if (getUser1Response == null)
            {
                await waitMessage.EditAsync(BotClient, language.error_specificUserNotFound.Fill([parameters[0]]));
                return;
            }

            if (getUser2Response == null)
            {
                await waitMessage.EditAsync(BotClient, language.error_specificUserNotFound.Fill([parameters[1]]));
                return;
            }

            var user1 = getUser1Response.UserExtend!;
            var user2 = getUser2Response.UserExtend!;

            string acc1 = $"{user1.Statistics!.HitAccuracy:N2}%";
            string acc2 = $"{user2.Statistics!.HitAccuracy:N2}%";

            int playtimeHours(int playtime) => playtime / 3600;
            int max = new[] 
            { 
                (user1.Statistics.CountryRank + "# UZ").Length, 
                (user1.Statistics.GlobalRank + "#").Length,
                (user1.Statistics.Pp!.Value.ToString("N2") + "pp").Length, 
                acc1.Length, $"{user1.Statistics.PlayTime}h".Length, 
                user1.Username!.Length 
            }.Max();

            string textToSend = language.command_compare.Fill([
                gamemode.ParseFromRuleset()!, 

                user1.Username.PadRight(max), 
                user2.Username!, 

                ("#" + user1.Statistics.GlobalRank.ReplaceIfNull()).PadRight(max), 
                $"#{user2.Statistics.GlobalRank.ReplaceIfNull()}", 

                ("#" + user1.Statistics.CountryRank.ReplaceIfNull() + " " + user1.CountryCode).PadRight(max), 
                "#" + user2.Statistics.CountryRank.ReplaceIfNull() + " " + user2.CountryCode, 

                (user1.Statistics.Pp!.Value.ToString("N2") + "pp").PadRight(max), 
                user2.Statistics.Pp!.Value.ToString("N2") + "pp", 

                acc1.PadRight(max), 
                acc2, 

                $"{(playtimeHours(user1.Statistics.PlayTime!.Value).ToString() + "h").PadRight(max)}", 
                $"{playtimeHours(user2.Statistics.PlayTime!.Value)}h"]);
            await waitMessage.EditAsync(BotClient, textToSend);
        }
    }
}
