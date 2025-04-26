using Microsoft.EntityFrameworkCore;
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
    public class OsuChatstatsCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/chatstats", "/stats"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);
            List<OsuUser> foundChatMembers = new List<OsuUser>();

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);
            string sendText = language.command_chatstats_title;

            foreach (var memberId in chatInDatabase!.ChatMembers!)
            {
                OsuUser? foundMember = await Database.OsuUsers.FindAsync(memberId);
                if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.TelegramId)) foundChatMembers.Add(foundMember);
            }
            foundChatMembers = foundChatMembers.Take(10).OrderByDescending(m => m.PPValue).ToList();

            int i = 1;
            foreach (OsuUser chatMember in foundChatMembers)
            {
                sendText += language.command_chatstats_row.Fill([$"{i}", $"{chatMember.OsuUsername}", $"{chatMember.PPValue:N0}"]);
                i += 1;
            }
            sendText += language.command_chatstats_end;
            await waitMessage.EditAsync(BotClient, sendText);
        }
    }
}
