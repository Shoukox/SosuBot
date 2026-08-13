using System.Globalization;
using Microsoft.Extensions.Logging;
using SosuBot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services;

public sealed class ChatModerationService(
    TelegramUserDirectory userDirectory,
    ILogger<ChatModerationService> logger)
{
    private const long CreatorTelegramId = 728384906;

    private static readonly HashSet<string> ModerationCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "kick", "remove", "softban", "ban", "unban", "mute", "silence", "restrict", "unmute", "unrestrict",
        "promote", "demote", "title"
    };

    private static readonly HashSet<string> DurationUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "s", "m", "h", "d", "w"
    };

    public static bool IsModerationCommand(string command) =>
        ModerationCommands.Contains(command);

    public static bool IsCreator(long userId) => userId == CreatorTelegramId;

    public async Task ExecuteAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        string command = parameters[0].ToLowerInvariant();

        if (message.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await message.ReplyAsync(botClient, "Эта команда работает только в группах и супергруппах.");
            return;
        }

        try
        {
            switch (command)
            {
                case "kick":
                case "remove":
                case "softban":
                    await KickAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "ban":
                    await BanAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "unban":
                    await UnbanAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "mute":
                case "silence":
                case "restrict":
                    await MuteAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "unmute":
                case "unrestrict":
                    await UnmuteAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "promote":
                    await PromoteAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "demote":
                    await DemoteAsync(botClient, message, parameters, cancellationToken);
                    break;
                case "title":
                    await SetTitleAsync(botClient, message, parameters, cancellationToken);
                    break;
            }
        }
        catch (ApiRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Telegram rejected moderation command {Command} in chat {ChatId}",
                command,
                message.Chat.Id);

            await message.ReplyAsync(
                botClient,
                "Telegram не выполнил действие. Проверь, что бот администратор чата и у него есть нужное право.");
        }
    }

    private async Task KickAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken);
        if (target is null || parameters.Length != target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "kick|remove <username|id> или ответом на сообщение");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "исключить", cancellationToken))
            return;

        // A ban followed by an unban removes the member without keeping them banned.
        await botClient.BanChatMember(
            message.Chat.Id,
            target.UserId,
            revokeMessages: false,
            cancellationToken: cancellationToken);
        await botClient.UnbanChatMember(
            message.Chat.Id,
            target.UserId,
            onlyIfBanned: false,
            cancellationToken: cancellationToken);

        await SendResultAsync(botClient, message, $"Пользователь {target.DisplayName} исключён из чата.");
    }

    private async Task BanAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: true,
            cancellationToken);
        if (target is null)
        {
            await SendUsageAsync(botClient, message, "ban <username|id> [срок] или ответом на сообщение");
            return;
        }

        if (!TryParseOptionalUntilDate(parameters, target.NextParameterIndex, out DateTime? untilDate))
        {
            await SendUsageAsync(botClient, message, "ban <username|id> [30m|2h|7d|forever]");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "заблокировать", cancellationToken))
            return;

        await botClient.BanChatMember(
            message.Chat.Id,
            target.UserId,
            untilDate: untilDate,
            revokeMessages: false,
            cancellationToken: cancellationToken);

        string durationText = untilDate is null ? "навсегда" : $"до {untilDate.Value:yyyy-MM-dd HH:mm} UTC";
        await SendResultAsync(botClient, message,
            $"Пользователь {target.DisplayName} заблокирован {durationText}.");
    }

    private async Task UnbanAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken);
        if (target is null || parameters.Length != target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "unban <username|id> или ответом на сообщение");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "разблокировать", cancellationToken))
            return;

        await botClient.UnbanChatMember(
            message.Chat.Id,
            target.UserId,
            onlyIfBanned: true,
            cancellationToken: cancellationToken);
        await SendResultAsync(botClient, message, $"Пользователь {target.DisplayName} разблокирован.");
    }

    private async Task MuteAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Supergroup)
        {
            await message.ReplyAsync(botClient, "Mute работает только в супергруппах Telegram.");
            return;
        }

        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: true,
            cancellationToken);
        if (target is null)
        {
            await SendUsageAsync(botClient, message, "mute <username|id> [срок] или ответом на сообщение");
            return;
        }

        if (!TryParseOptionalUntilDate(parameters, target.NextParameterIndex, out DateTime? untilDate))
        {
            await SendUsageAsync(botClient, message, "mute <username|id> [30m|2h|7d|forever]");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "замьютить", cancellationToken))
            return;

        await botClient.RestrictChatMember(
            message.Chat.Id,
            target.UserId,
            new ChatPermissions(),
            useIndependentChatPermissions: true,
            untilDate: untilDate,
            cancellationToken: cancellationToken);

        string durationText = untilDate is null ? "навсегда" : $"до {untilDate.Value:yyyy-MM-dd HH:mm} UTC";
        await SendResultAsync(botClient, message,
            $"Пользователь {target.DisplayName} замьючен {durationText}.");
    }

    private async Task UnmuteAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Supergroup)
        {
            await message.ReplyAsync(botClient, "Unmute работает только в супергруппах Telegram.");
            return;
        }

        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken);
        if (target is null || parameters.Length != target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "unmute <username|id> или ответом на сообщение");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "размьютить", cancellationToken))
            return;

        await botClient.RestrictChatMember(
            message.Chat.Id,
            target.UserId,
            new ChatPermissions(true),
            useIndependentChatPermissions: true,
            untilDate: null,
            cancellationToken: cancellationToken);
        await SendResultAsync(botClient, message, $"Пользователь {target.DisplayName} размьючен.");
    }

    private async Task PromoteAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Supergroup)
        {
            await message.ReplyAsync(botClient, "Promote работает только в супергруппах Telegram.");
            return;
        }

        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken);
        if (target is null || parameters.Length != target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "promote <username|id> или ответом на сообщение");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "назначить администратором", cancellationToken))
            return;

        // Deliberately use a limited, useful default set of rights. The creator
        // can grant the dangerous promotion right later from Telegram itself.
        await botClient.PromoteChatMember(
            message.Chat.Id,
            target.UserId,
            isAnonymous: false,
            canManageChat: true,
            canPostMessages: false,
            canEditMessages: false,
            canDeleteMessages: true,
            canPostStories: false,
            canEditStories: false,
            canDeleteStories: false,
            canManageVideoChats: true,
            canRestrictMembers: true,
            canPromoteMembers: false,
            canChangeInfo: false,
            canInviteUsers: true,
            canPinMessages: true,
            canManageTopics: true,
            canManageDirectMessages: false,
            canManageTags: false,
            cancellationToken: cancellationToken);
        await SendResultAsync(botClient, message, $"Пользователь {target.DisplayName} назначен администратором.");
    }

    private async Task DemoteAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Supergroup)
        {
            await message.ReplyAsync(botClient, "Demote работает только в супергруппах Telegram.");
            return;
        }

        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken);
        if (target is null || parameters.Length != target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "demote <username|id> или ответом на сообщение");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "снять права администратора у", cancellationToken))
            return;

        await botClient.PromoteChatMember(
            message.Chat.Id,
            target.UserId,
            isAnonymous: false,
            canManageChat: false,
            canPostMessages: false,
            canEditMessages: false,
            canDeleteMessages: false,
            canPostStories: false,
            canEditStories: false,
            canDeleteStories: false,
            canManageVideoChats: false,
            canRestrictMembers: false,
            canPromoteMembers: false,
            canChangeInfo: false,
            canInviteUsers: false,
            canPinMessages: false,
            canManageTopics: false,
            canManageDirectMessages: false,
            canManageTags: false,
            cancellationToken: cancellationToken);
        await SendResultAsync(botClient, message, $"Права администратора у пользователя {target.DisplayName} сняты.");
    }

    private async Task SetTitleAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Supergroup)
        {
            await message.ReplyAsync(botClient, "Title работает только в супергруппах Telegram.");
            return;
        }

        TargetResolution? target = await ResolveTargetAsync(
            botClient,
            message,
            parameters,
            allowDurationFromReply: false,
            cancellationToken,
            allowAnyArgumentFromReply: true);
        if (target is null || parameters.Length <= target.NextParameterIndex)
        {
            await SendUsageAsync(botClient, message, "title <username|id> <титул> или ответом на сообщение");
            return;
        }

        string title = string.Join(" ", parameters[target.NextParameterIndex..]);
        if (title == "-") title = string.Empty;
        if (title.Length > 16)
        {
            await message.ReplyAsync(botClient, "Титул Telegram должен быть не длиннее 16 символов.");
            return;
        }

        if (!await CanModifyTargetAsync(botClient, message, target.UserId, "изменить титул у", cancellationToken))
            return;

        await botClient.SetChatAdministratorCustomTitle(
            message.Chat.Id,
            target.UserId,
            title,
            cancellationToken: cancellationToken);
        await SendResultAsync(botClient, message,
            $"Титул пользователя {target.DisplayName} установлен: {(title.Length == 0 ? "снят" : title.EncodeHtml())}.");
    }

    private async Task<TargetResolution?> ResolveTargetAsync(
        ITelegramBotClient botClient,
        Message message,
        string[] parameters,
        bool allowDurationFromReply,
        CancellationToken cancellationToken,
        bool allowAnyArgumentFromReply = false)
    {
        int parameterIndex = 1;
        string? explicitTarget = parameters.Length > parameterIndex ? parameters[parameterIndex] : null;

        if (message.ReplyToMessage?.From is { } repliedUser &&
            (explicitTarget is null ||
             allowAnyArgumentFromReply ||
             allowDurationFromReply && IsDuration(explicitTarget)))
        {
            userDirectory.Remember(repliedUser);
            return CreateTarget(repliedUser.Id, repliedUser.Username, parameterIndex);
        }

        if (explicitTarget is null)
            return null;

        if (TryParseTelegramId(explicitTarget, out long userId))
            return CreateTarget(userId, null, parameterIndex + 1);

        User? mentionedUser = GetTextMentionedUser(message, explicitTarget);
        if (mentionedUser is not null)
        {
            userDirectory.Remember(mentionedUser);
            return CreateTarget(mentionedUser.Id, mentionedUser.Username, parameterIndex + 1);
        }

        if (userDirectory.TryGetUserId(explicitTarget, out userId))
            return CreateTarget(userId, explicitTarget.TrimStart('@'), parameterIndex + 1);

        // The Bot API cannot search ordinary members by username. It can,
        // however, return administrators, so use that as a useful fallback.
        try
        {
            ChatMember[] administrators = await botClient.GetChatAdministrators(
                message.Chat.Id,
                cancellationToken: cancellationToken);
            foreach (ChatMember administrator in administrators)
                userDirectory.Remember(administrator.User);

            if (userDirectory.TryGetUserId(explicitTarget, out userId))
                return CreateTarget(userId, explicitTarget.TrimStart('@'), parameterIndex + 1);
        }
        catch (ApiRequestException exception)
        {
            logger.LogDebug(
                exception,
                "Could not resolve username {Username} through chat administrators in {ChatId}",
                explicitTarget,
                message.Chat.Id);
        }

        await message.ReplyAsync(
            botClient,
            $"Не удалось найти {explicitTarget.EncodeHtml()}. Ответь на сообщение пользователя или укажи его числовой Telegram ID. " +
            "Username работает, если бот уже видел этого пользователя.");
        return null;
    }

    private async Task<bool> CanModifyTargetAsync(
        ITelegramBotClient botClient,
        Message message,
        long targetUserId,
        string action,
        CancellationToken cancellationToken)
    {
        if (targetUserId == CreatorTelegramId)
        {
            await message.ReplyAsync(botClient, "Создателя бота нельзя изменить этой командой.");
            return false;
        }

        if (targetUserId == botClient.BotId)
        {
            await message.ReplyAsync(botClient, "Нельзя применить эту команду к самому боту.");
            return false;
        }

        try
        {
            ChatMember member = await botClient.GetChatMember(
                message.Chat.Id,
                targetUserId,
                cancellationToken: cancellationToken);
            if (member.Status == ChatMemberStatus.Creator)
            {
                await message.ReplyAsync(botClient, "Владельца чата нельзя изменить этой командой.");
                return false;
            }

            if (member.Status == ChatMemberStatus.Administrator &&
                action is "исключить" or "заблокировать" or "замьютить")
            {
                await message.ReplyAsync(botClient,
                    "Сначала сними с пользователя права администратора командой /c demote.");
                return false;
            }
        }
        catch (ApiRequestException exception) when (IsMissingMemberError(exception))
        {
            if (action is "замьютить" or "назначить администратором" or "изменить титул у")
            {
                await message.ReplyAsync(botClient, "Пользователь не найден в этом чате.");
                return false;
            }
        }

        return true;
    }

    private static TargetResolution CreateTarget(long userId, string? username, int nextParameterIndex) =>
        new(userId, string.IsNullOrWhiteSpace(username) ? $"<code>{userId}</code>" : $"@{username.TrimStart('@').EncodeHtml()}", nextParameterIndex);

    private static async Task SendUsageAsync(
        ITelegramBotClient botClient,
        Message message,
        string usage)
    {
        await message.ReplyAsync(botClient, $"Использование: <code>/c {usage}</code>");
    }

    private static Task SendResultAsync(
        ITelegramBotClient botClient,
        Message message,
        string text) => message.ReplyAsync(botClient, text);

    private static bool TryParseTelegramId(string value, out long userId)
    {
        string normalized = value.Trim().TrimStart('@');
        return long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out userId) &&
               userId > 0;
    }

    private static bool TryParseOptionalUntilDate(
        string[] parameters,
        int parameterIndex,
        out DateTime? untilDate)
    {
        untilDate = null;
        if (parameters.Length == parameterIndex)
            return true;
        if (parameters.Length != parameterIndex + 1)
            return false;

        string value = parameters[parameterIndex].Trim();
        if (value.Equals("forever", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("permanent", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("perm", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!TryParseDuration(value, out TimeSpan duration))
            return false;

        untilDate = DateTime.UtcNow.Add(duration);
        return true;
    }

    private static bool IsDuration(string value) =>
        value.Equals("forever", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("permanent", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("perm", StringComparison.OrdinalIgnoreCase) ||
        TryParseDuration(value, out _);

    private static bool TryParseDuration(string value, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        int position = 0;
        double totalSeconds = 0;
        while (position < value.Length)
        {
            int numberStart = position;
            while (position < value.Length && (char.IsDigit(value[position]) || value[position] == '.'))
                position++;
            if (numberStart == position ||
                !double.TryParse(value[numberStart..position], NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out double amount) ||
                position >= value.Length)
                return false;

            string unit = value[position].ToString().ToLowerInvariant();
            position++;
            if (!DurationUnits.Contains(unit) || amount <= 0)
                return false;

            totalSeconds += unit switch
            {
                "s" => amount,
                "m" => amount * 60,
                "h" => amount * 60 * 60,
                "d" => amount * 24 * 60 * 60,
                "w" => amount * 7 * 24 * 60 * 60,
                _ => 0
            };

            if (totalSeconds > TimeSpan.FromDays(366).TotalSeconds)
                return false;
        }

        duration = TimeSpan.FromSeconds(totalSeconds);
        return duration >= TimeSpan.FromSeconds(30);
    }

    private static User? GetTextMentionedUser(Message message, string target)
    {
        if (message.Text is null || message.Entities is null)
            return null;

        foreach (MessageEntity entity in message.Entities.Where(e => e.Type == MessageEntityType.TextMention))
        {
            if (entity.User is null || entity.Offset < 0 || entity.Length <= 0 ||
                entity.Offset + entity.Length > message.Text.Length)
                continue;

            string entityText = message.Text.Substring(entity.Offset, entity.Length);
            if (string.Equals(entityText, target, StringComparison.OrdinalIgnoreCase))
                return entity.User;
        }

        return null;
    }

    private static bool IsMissingMemberError(ApiRequestException exception) =>
        exception.Message.Contains("user not found", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("participant_id_invalid", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("user_id_invalid", StringComparison.OrdinalIgnoreCase);

    private sealed record TargetResolution(long UserId, string DisplayName, int NextParameterIndex);
}
