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
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class OsuModeCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/mode"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            string msgText = Context.Text!;
            string[] parameters = msgText.GetCommandParameters()!;
            string? osuMode = parameters[0].ParseToRuleset();

            if (osuMode is null)
            {
                await Context.ReplyAsync(BotClient, language.error_modeIncorrect);
                return;
            }
            if (osuUserInDatabase is null)
            {
                await Context.ReplyAsync(BotClient, language.error_noUser);
                return;
            }

            if (string.IsNullOrEmpty(osuMode))
            {
                await Context.ReplyAsync(BotClient, language.error_modeIsEmpty);
                return;
            }

            osuUserInDatabase.OsuMode = osuMode;

            string sendText = language.command_setMode.Fill([osuUserInDatabase.OsuMode]);
            await Context.ReplyAsync(BotClient, sendText);
        }
    }
}
