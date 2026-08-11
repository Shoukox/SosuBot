using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Localization;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Extensions;

public static class BotContextExtensions
{
    public static async Task AddOrUpdateTelegramChat(
        this BotContext database,
        Message message,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await AddOrUpdateTelegramChat(
            database,
            message.Chat.Id,
            message.From?.Id,
            message.LeftChatMember?.Id,
            message.NewChatMembers?.Select(member => member.Id) ?? [],
            message.From?.LanguageCode,
            message.Chat.Type,
            logger,
            cancellationToken);
    }

    public static async Task AddOrUpdateTelegramChat(
        this BotContext database,
        ChatMemberUpdated memberUpdate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        long memberId = memberUpdate.NewChatMember.User.Id;
        bool isInChat = memberUpdate.NewChatMember.IsInChat;

        await AddOrUpdateTelegramChat(
            database,
            memberUpdate.Chat.Id,
            isInChat ? memberId : null,
            isInChat ? null : memberId,
            [],
            memberUpdate.NewChatMember.User.LanguageCode ?? memberUpdate.From.LanguageCode,
            memberUpdate.Chat.Type,
            logger,
            cancellationToken);
    }

    private static async Task AddOrUpdateTelegramChat(
        BotContext database,
        long chatId,
        long? userId,
        long? leftUserId,
        IEnumerable<long> newMemberIds,
        string? telegramLanguageCode,
        ChatType chatType,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var defaultLanguage = Language.English;
        var membersToAdd = new HashSet<long>(newMemberIds);
        if (userId is not null)
            membersToAdd.Add(userId.Value);

        if (leftUserId is not null)
            membersToAdd.Remove(leftUserId.Value);

        try
        {
            TelegramChat? chat = await database.TelegramChats.FindAsync([chatId], cancellationToken);
            if (chat == null)
            {
                if (chatType is ChatType.Private)
                {
                    defaultLanguage = telegramLanguageCode switch
                    {
                        var code when !string.IsNullOrWhiteSpace(code) && code.StartsWith(Language.Russian, StringComparison.OrdinalIgnoreCase) => Language.Russian,
                        var code when !string.IsNullOrWhiteSpace(code) && code.StartsWith(Language.English, StringComparison.OrdinalIgnoreCase) => Language.English,
                        var code when !string.IsNullOrWhiteSpace(code) && code.StartsWith(Language.German, StringComparison.OrdinalIgnoreCase) => Language.German,
                        _ => Language.English
                    };
                }

                await database.AddAsync(new TelegramChat
                {
                    ChatId = chatId,
                    ChatMembers = membersToAdd.ToList(),
                    LastBeatmapId = null,
                    LanguageCode = defaultLanguage
                }, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                return;
            }

            chat.ChatMembers ??= [];
            var normalizedMembers = chat.ChatMembers.Distinct().ToList();
            bool membersChanged = normalizedMembers.Count != chat.ChatMembers.Count;
            if (membersChanged)
                chat.ChatMembers = normalizedMembers;

            bool languageChanged = false;
            if (string.IsNullOrWhiteSpace(chat.LanguageCode))
            {
                chat.LanguageCode = defaultLanguage;
                languageChanged = true;
            }

            if (leftUserId is not null)
            {
                membersChanged |= chat.ChatMembers.RemoveAll(member => member == leftUserId.Value) > 0;
            }

            foreach (long newMemberId in membersToAdd)
            {
                if (chat.ChatMembers.Contains(newMemberId))
                    continue;

                chat.ChatMembers.Add(newMemberId);
                membersChanged = true;
            }

            if (membersChanged || languageChanged)
                await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pe && pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in dbEx.Entries)
                entry.State = EntityState.Detached;

            logger.LogDebug("Ignored a concurrent Telegram chat insert");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }
}
