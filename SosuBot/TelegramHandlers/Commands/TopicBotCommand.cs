using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class TopicBotCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/topicbot"];
    public static readonly string Description = "админ: управление ответами бота в темах";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        if (Context.Update.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForGroups);
            return;
        }

        if (Context.Update.From is not { } user ||
            !await TelegramHelper.IsGroupAdmin(Context.BotClient, Context.Update.Chat, user.Id))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForAdmins);
            return;
        }

        string commandText = Context.Update.Text!;
        int commandEnd = commandText.IndexOf(' ');
        string[] parameters = commandEnd < 0
            ? []
            : commandText[(commandEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parameters.Length == 0)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.topicBot_usage);
            return;
        }

        BotContext database = Context.ServiceProvider.GetRequiredService<BotContext>();
        SosuBot.Database.Models.TelegramChat? chat = await database.TelegramChats
            .FirstOrDefaultAsync(chat => chat.ChatId == Context.Update.Chat.Id, Context.CancellationToken);

        switch (parameters[0].ToLowerInvariant())
        {
            case "list" when parameters.Length == 1:
                await ReplyBlockedTopicsAsync(chat?.BlockedTopicIds, language);
                return;
            case "deny":
            case "allow":
                await UpdateTopicPermissionAsync(parameters, chat, database, language);
                return;
            default:
                await Context.Update.ReplyAsync(Context.BotClient, language.topicBot_usage);
                return;
        }
    }

    private async Task UpdateTopicPermissionAsync(
        string[] parameters,
        SosuBot.Database.Models.TelegramChat? chat,
        BotContext database,
        ILocalization language)
    {
        if (!TryResolveTopicId(parameters, language, out int topicId, out string errorMessage))
        {
            await Context.Update.ReplyAsync(Context.BotClient, errorMessage);
            return;
        }

        chat = GetOrCreateChat(chat, database);
        List<int> blockedTopicIds = chat.BlockedTopicIds ??= [];

        bool deny = parameters[0].Equals("deny", StringComparison.OrdinalIgnoreCase);
        string result = UpdateTopicPermission(blockedTopicIds, topicId, deny, language);
        await Context.Update.ReplyAsync(Context.BotClient, result);
    }

    private bool TryResolveTopicId(
        string[] parameters,
        ILocalization language,
        out int topicId,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (parameters.Length > 2)
        {
            topicId = default;
            errorMessage = language.topicBot_usage;
            return false;
        }

        if (parameters.Length == 2)
        {
            if (int.TryParse(parameters[1], NumberStyles.None, CultureInfo.InvariantCulture, out topicId) &&
                topicId > 0)
                return true;

            errorMessage = language.topicBot_invalidTopicId;
            return false;
        }

        if (Context.Update.MessageThreadId is { } currentTopicId && currentTopicId > 0)
        {
            topicId = currentTopicId;
            return true;
        }

        topicId = default;
        errorMessage = language.topicBot_topicRequired;
        return false;
    }

    private SosuBot.Database.Models.TelegramChat GetOrCreateChat(
        SosuBot.Database.Models.TelegramChat? chat,
        BotContext database)
    {
        if (chat is not null)
            return chat;

        chat = new SosuBot.Database.Models.TelegramChat { ChatId = Context.Update.Chat.Id };
        database.TelegramChats.Add(chat);
        return chat;
    }

    private static string UpdateTopicPermission(
        List<int> blockedTopicIds,
        int topicId,
        bool deny,
        ILocalization language)
    {
        if (deny)
        {
            if (blockedTopicIds.Contains(topicId))
                return $"{language.topicBot_alreadyBlocked} <code>{topicId}</code>.";

            blockedTopicIds.Add(topicId);
            return $"{language.topicBot_blocked} <code>{topicId}</code>.";
        }

        return blockedTopicIds.Remove(topicId)
            ? $"{language.topicBot_allowed} <code>{topicId}</code>."
            : $"{language.topicBot_notBlocked}: <code>{topicId}</code>.";
    }

    private async Task ReplyBlockedTopicsAsync(List<int>? blockedTopicIds, ILocalization language)
    {
        if (blockedTopicIds is not { Count: > 0 })
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.topicBot_noBlockedTopics);
            return;
        }

        string topicIds = string.Join(", ", blockedTopicIds.Order());
        await Context.Update.ReplyAsync(
            Context.BotClient,
            $"{language.topicBot_blockedTopics}\n<code>{topicIds}</code>");
    }
}
