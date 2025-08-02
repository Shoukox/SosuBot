using Microsoft.EntityFrameworkCore;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuSetCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/set"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();

            string msgText = Context.Update.Text!;
            string[] parameters = msgText.GetCommandParameters()!;
            string osuUsername = string.Join(' ', parameters);

            if (string.IsNullOrEmpty(osuUsername))
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_nameIsEmpty);
                return;
            }

            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FirstOrDefaultAsync(m => m.TelegramId == Context.Update.From!.Id);
            GetUserResponse? response = await Context.OsuApiV2.Users.GetUser($"@{osuUsername}", new());
            if (response is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotFound);
                return;
            }

            UserExtend user = response.UserExtend!;

            Playmode playmode = user.Playmode!.ParseRulesetToPlaymode();
            if (osuUserInDatabase is null)
            {
                OsuUser newOsuUser = new OsuUser()
                {
                    TelegramId = Context.Update.From!.Id,
                    OsuUsername = user.Username!,
                    OsuUserId = user.Id.Value,
                    OsuMode = playmode
                };
                newOsuUser.SetPP(user.Statistics!.Pp!.Value, playmode);

                await Context.Database.OsuUsers.AddAsync(newOsuUser);
                osuUserInDatabase = newOsuUser;
            }
            else
            {
                osuUserInDatabase.Update(user, playmode);
            }
            string sendText = language.command_set.Fill([$"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}", $"{osuUserInDatabase.GetPP(playmode):N2}", osuUserInDatabase.OsuMode.ToGamemode()!]);
            await Context.Update.ReplyAsync(Context.BotClient, sendText);
        }
    }
}
