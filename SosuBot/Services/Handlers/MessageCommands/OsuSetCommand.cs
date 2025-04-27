using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Scores.Models;
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

namespace SosuBot.Services.Handlers.MessageCommands
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

            OsuUser? osuUserInDatabase = await Database.OsuUsers.FirstOrDefaultAsync(m => m.OsuUsername == osuUsername);
            GetUserResponse? response = await OsuApiV2.Users.GetUser($"@{osuUsername}", new());
            if (response is null)
            {
                await Context.ReplyAsync(BotClient, language.error_userNotFound);
                return;
            }

            if (osuUserInDatabase is null)
            {
                OsuUser newOsuUser = new OsuUser()
                {
                    TelegramId = Context.From!.Id, 
                    OsuUsername = response.UserExtend!.Username!,
                    OsuUserId = response.UserExtend!.Id.Value,
                    PPValue = response.UserExtend.Statistics!.Pp,
                    OsuMode = Ruleset.Osu
                };
                await Database.OsuUsers.AddAsync(newOsuUser);

                osuUserInDatabase = newOsuUser;
            }
            else
            {
                osuUserInDatabase.TelegramId = Context.From!.Id;
                osuUserInDatabase.OsuUsername = response.UserExtend!.Username!;
                osuUserInDatabase.OsuUserId = response.UserExtend!.Id.Value;
                osuUserInDatabase.PPValue = response.UserExtend.Statistics!.Pp;
            }
            string sendText = language.command_set.Fill([$"{response.UserExtend!.Username!}", osuUserInDatabase.OsuMode.ParseFromRuleset()!]);
            await Context.ReplyAsync(BotClient, sendText);
        }
    }
}
