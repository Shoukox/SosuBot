using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Localization;
using Telegram.Bot.Types;

namespace SosuBot.Extensions;

public static class BotContextExtensions
{
    public static async Task AddOrUpdateTelegramChat(this BotContext database, Message message, ILogger? logger = null)
    {
        var chatId = message.Chat.Id;
        var userId = message.From?.Id;
        var leftUserId = message.LeftChatMember?.Id;
        var telegramLanguageCode = message.From?.LanguageCode;
        var defaultLanguage = telegramLanguageCode switch
        {
            var code when !string.IsNullOrWhiteSpace(code) && code.StartsWith(Language.English, StringComparison.OrdinalIgnoreCase) => Language.English,
            var code when !string.IsNullOrWhiteSpace(code) && code.StartsWith(Language.German, StringComparison.OrdinalIgnoreCase) => Language.German,
            _ => Language.Russian
        };

        try
        {
            var chat = await database.TelegramChats.FindAsync(chatId);
            if (chat == null)
            {
                await database.AddAsync(new TelegramChat
                {
                    ChatId = chatId,
                    ChatMembers = userId is null ? [] : [userId.Value],
                    LastBeatmapId = null,
                    LanguageCode = defaultLanguage
                });
                await database.SaveChangesAsync();
                return;
            }

            chat.ChatMembers ??= [];
            if (string.IsNullOrWhiteSpace(chat.LanguageCode))
                chat.LanguageCode = defaultLanguage;

            if (leftUserId is not null)
            {
                chat.ChatMembers.Remove(leftUserId.Value);
                await database.SaveChangesAsync();
                return;
            }

            if (userId is not null && !chat.ChatMembers.Contains(userId.Value))
            {
                chat.ChatMembers.Add(userId.Value);
                await database.SaveChangesAsync();
            }
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pe && pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // because of a thread race
            // do nothing
        }
        catch (Exception e)
        {
            logger?.LogError(e.ToString());
        }
    }
}