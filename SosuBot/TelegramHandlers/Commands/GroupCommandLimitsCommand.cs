using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Callbacks;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class GroupCommandLimitsCommand : CommandBase<Message>
{
    public static readonly string[] Commands = [BotCommandCatalog.LimitsCommand];
    public static readonly string Description = "панель ограничений группы";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        Message message = Context.Update;
        if (message.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await message.ReplyAsync(Context.BotClient, language.group_onlyForGroups);
            return;
        }

        if (message.From is not { } user ||
            (await Context.BotClient.GetChatMember(message.Chat.Id, user.Id, Context.CancellationToken)).Status !=
            ChatMemberStatus.Creator)
        {
            await SendPrivateNoticeAsync(message.Chat.Id, message.From?.Id, language.groupLimits_ownerOnly);
            return;
        }

        BotContext database = Context.ServiceProvider.GetRequiredService<BotContext>();
        TelegramChat? settings = await database.TelegramChats
            .FirstOrDefaultAsync(chat => chat.ChatId == message.Chat.Id, Context.CancellationToken);

        await Context.BotClient.SendMessage(
            message.Chat.Id,
            BuildText(settings, language),
            replyMarkup: BuildMainKeyboard(settings, language),
            ephemeralMessageParameters: new EphemeralMessageParameters { ReceiverUserId = user.Id },
            cancellationToken: Context.CancellationToken);
    }

    internal static string BuildText(TelegramChat? settings, ILocalization language)
    {
        string status = settings?.CommandRestrictionsEnabled == true
            ? language.groupLimits_enabled
            : language.groupLimits_disabled;
        string files = settings?.IgnoreMedia == true
            ? language.groupLimits_mediaDisabled
            : language.groupLimits_mediaEnabled;

        return $"{language.groupLimits_title}\n" +
               $"{status}\n" +
               $"{string.Format(language.groupLimits_cooldown, settings?.CommandCooldownSeconds ?? 0)}\n" +
               $"{files}";
    }

    internal static InlineKeyboardMarkup BuildMainKeyboard(TelegramChat? settings, ILocalization language) =>
        new([
            [
                InlineKeyboardButton.WithCallbackData(
                    settings?.CommandRestrictionsEnabled == true
                        ? language.groupLimits_disable
                        : language.groupLimits_enable,
                    $"{GroupCommandLimitsCallback.Command} toggle")
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    string.Format(language.groupLimits_cooldown, settings?.CommandCooldownSeconds ?? 0),
                    $"{GroupCommandLimitsCallback.Command} cooldown")
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    settings?.IgnoreMedia == true
                        ? language.groupLimits_mediaDisabled
                        : language.groupLimits_mediaEnabled,
                    $"{GroupCommandLimitsCallback.Command} media")
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    language.groupLimits_categories,
                    $"{GroupCommandLimitsCallback.Command} categories")
            ]
        ]);

    private async Task SendPrivateNoticeAsync(long chatId, long? userId, string text)
    {
        if (userId is { } receiverUserId)
        {
            await Context.BotClient.SendMessage(
                chatId,
                text,
                ephemeralMessageParameters: new EphemeralMessageParameters { ReceiverUserId = receiverUserId },
                cancellationToken: Context.CancellationToken);
        }
    }
}
