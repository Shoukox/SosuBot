using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Users.Models;
using OsuApi.Core.V2.Users.Models.HttpIO;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.OsuTypes;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuSetCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/set"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();

            string msgText = Context.Text!;
            string[] parameters = msgText.GetCommandParameters()!;
            string osuUsername = string.Join(' ', parameters);

            if (string.IsNullOrEmpty(osuUsername))
            {
                await Context.ReplyAsync(BotClient, language.error_nameIsEmpty);
                return;
            }

            OsuUser? osuUserInDatabase = await Database.OsuUsers.FirstOrDefaultAsync(m => m.TelegramId == Context.From!.Id);
            GetUserResponse? response = await OsuApiV2.Users.GetUser($"@{osuUsername}", new());
            if (response is null)
            {
                await Context.ReplyAsync(BotClient, language.error_userNotFound);
                return;
            }


            UserExtend user = response.UserExtend!;

            Playmode playmode = user.Playmode!.ParseRulesetToPlaymode();
            if (osuUserInDatabase is null)
            {
                OsuUser newOsuUser = new OsuUser()
                {
                    TelegramId = Context.From!.Id,
                    OsuUsername = user.Username!,
                    OsuUserId = user.Id.Value,
                    OsuMode = playmode
                };
                newOsuUser.SetPP(user.Statistics!.Pp!.Value, playmode);

                await Database.OsuUsers.AddAsync(newOsuUser);
                osuUserInDatabase = newOsuUser;
            }
            else
            {
                osuUserInDatabase.Update(user, playmode);
            }
            string sendText = language.command_set.Fill([$"{user.Username!}", $"{osuUserInDatabase.GetPP(playmode):N2}", osuUserInDatabase.OsuMode.ToGamemode()!]);
            await Context.ReplyAsync(BotClient, sendText);
        }
    }
}
