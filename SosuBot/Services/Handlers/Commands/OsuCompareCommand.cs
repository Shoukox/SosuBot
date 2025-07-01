using OsuApi.Core.V2.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuCompareCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/compare", "/cmp"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
            List<OsuUser> foundChatMembers = new List<OsuUser>();

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            if (parameters.Length < 2)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
                return;
            }

            string? ruleset = Ruleset.Osu;
            if (parameters.Length >= 3)
            {
                ruleset = parameters[2].ParseToRuleset();
                if (ruleset is null)
                {
                    await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }
            }

            var getUser1Response = await Context.OsuApiV2.Users.GetUser($"@{parameters[0]}", new(), mode: ruleset);
            var getUser2Response = await Context.OsuApiV2.Users.GetUser($"@{parameters[1]}", new(), mode: ruleset);

            if (getUser1Response == null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_specificUserNotFound.Fill([parameters[0]]));
                return;
            }

            if (getUser2Response == null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_specificUserNotFound.Fill([parameters[1]]));
                return;
            }

            var user1 = getUser1Response.UserExtend!;
            var user2 = getUser2Response.UserExtend!;

            string acc1 = $"{user1.Statistics!.HitAccuracy:N2}%";
            string acc2 = $"{user2.Statistics!.HitAccuracy:N2}%";

            int max = new[]
            {
                (user1.Statistics.CountryRank + "# UZ").Length,
                (user1.Statistics.GlobalRank + "#").Length,
                (user1.Statistics.Pp!.Value.ToString("N2") + "pp").Length,
                acc1.Length, $"{user1.Statistics.PlayTime}h".Length,
                user1.Username!.Length
            }.Max();

            string textToSend = language.command_compare.Fill([
                ruleset.ParseRulesetToGamemode(),

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

                $"{((user1.Statistics.PlayTime!.Value / 3600).ToString() + "h").PadRight(max)}",
                $"{user2.Statistics.PlayTime!.Value / 3600}h"]);
            await waitMessage.EditAsync(Context.BotClient, textToSend);
        }
    }
}
