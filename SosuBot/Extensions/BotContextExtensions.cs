using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Synchronization;
using Telegram.Bot.Types;

namespace SosuBot.Extensions
{
    public static class BotContextExtensions
    {
        public static async Task AddOrUpdateTelegramChat(this BotContext database, Message message)
        {
            var semaphoreSlim = BotSynchronization.Instance.GetSemaphoreSlim(message.Chat.Id);
            await semaphoreSlim.WaitAsync();
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
            semaphoreSlim.Release();
        }
    }
}
