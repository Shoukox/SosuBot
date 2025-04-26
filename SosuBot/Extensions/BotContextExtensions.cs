using SosuBot.Database;
using SosuBot.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Extensions
{
    public static class BotContextExtensions
    {
        public static async Task AddOrUpdateTelegramChat(this BotContext database, Message message)
        {
            if (await database.TelegramChats.FindAsync(message.Chat.Id) is TelegramChat chat)
            {
                chat.ChatMembers = chat.ChatMembers ?? new List<long>();
                if (!chat.ChatMembers!.Contains(message.From!.Id)) chat.ChatMembers.Add(message.From!.Id);
            }
            else
            {
                TelegramChat newChat = new TelegramChat()
                {
                    ChatId = message.Chat!.Id,
                    ChatMembers = new List<long>(),
                    LastBeatmapId = null
                };
                await database.AddAsync(newChat);
            }
        }
    }
}
