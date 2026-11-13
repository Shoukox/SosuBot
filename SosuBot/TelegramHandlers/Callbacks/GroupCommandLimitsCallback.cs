using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using SosuBot.TelegramHandlers.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Callbacks;

public sealed class GroupCommandLimitsCallback : CommandBase<CallbackQuery>
{
    public const string Command = "gcl";
    private static readonly int[] CooldownOptions = [0, 5, 10, 30, 60];

    public override async Task ExecuteAsync()
    {
        CallbackQuery callback = Context.Update;
        ILocalization language = Context.GetLocalization();
        Message? panel = callback.Message;
        if (panel is null || panel.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await callback.AnswerAsync(Context.BotClient, language.group_onlyForGroups, showAlert: true);
            return;
        }

        if ((await Context.BotClient.GetChatMember(panel.Chat.Id, callback.From.Id, Context.CancellationToken)).Status !=
            ChatMemberStatus.Creator)
        {
            await callback.AnswerAsync(Context.BotClient, language.groupLimits_ownerOnly, showAlert: true);
            return;
        }

        if (panel.EphemeralMessageId is not { } ephemeralMessageId ||
            panel.ReceiverUser?.Id != callback.From.Id)
        {
            await callback.AnswerAsync(Context.BotClient, language.groupLimits_closed, showAlert: true);
            return;
        }

        BotContext database = Context.ServiceProvider.GetRequiredService<BotContext>();
        TelegramChat settings = await database.TelegramChats
            .FirstOrDefaultAsync(chat => chat.ChatId == panel.Chat.Id, Context.CancellationToken)
            ?? CreateChat(panel.Chat.Id, database);

        string[] data = (callback.Data ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string action = data.Length > 1 ? data[1] : "home";
        string? argument = data.Length > 2 ? data[2] : null;
        string view = "home";
        BotCommandCategory? selectedCategory = null;
        bool changed = true;

        switch (action)
        {
            case "toggle":
                settings.CommandRestrictionsEnabled = !settings.CommandRestrictionsEnabled;
                break;
            case "cooldown":
                settings.CommandCooldownSeconds = NextCooldown(settings.CommandCooldownSeconds);
                break;
            case "media":
                settings.IgnoreMedia = !settings.IgnoreMedia;
                break;
            case "categories":
                view = "categories";
                changed = false;
                break;
            case "category" when TryParseCategory(argument, out BotCommandCategory selected):
                selectedCategory = selected;
                view = "category";
                changed = false;
                break;
            case "category-toggle" when TryParseCategory(argument, out BotCommandCategory categoryToToggle):
                BotCommandCatalog.ToggleCategory(settings.DisabledCommands ??= [], categoryToToggle);
                selectedCategory = categoryToToggle;
                view = "category";
                break;
            case "command" when argument is not null:
                BotCommandCatalog.ToggleCommand(settings.DisabledCommands ??= [], argument);
                if (BotCommandCatalog.GetCategoryForCommand(argument) is { } commandCategory)
                {
                    selectedCategory = commandCategory;
                    view = "category";
                }
                break;
            case "noop":
                await callback.AnswerAsync(Context.BotClient);
                return;
            case "close":
                await Context.BotClient.EditEphemeralMessageText(
                    panel.Chat.Id,
                    callback.From.Id,
                    ephemeralMessageId,
                    language.groupLimits_closed,
                    replyMarkup: new InlineKeyboardMarkup([]),
                    cancellationToken: Context.CancellationToken);
                await callback.AnswerAsync(Context.BotClient);
                return;
            case "home":
                changed = false;
                break;
            default:
                await callback.AnswerAsync(Context.BotClient, language.groupLimits_closed, showAlert: true);
                return;
        }

        if (changed)
        {
            await database.SaveChangesAsync(Context.CancellationToken);
            await SynchronizeCommandsAsync(panel.Chat.Id, settings);
        }

        await EditPanelAsync(panel, callback.From.Id, ephemeralMessageId, settings, view, selectedCategory, language);
        await callback.AnswerAsync(Context.BotClient);
    }

    private static TelegramChat CreateChat(long chatId, BotContext database)
    {
        TelegramChat chat = new() { ChatId = chatId };
        database.TelegramChats.Add(chat);
        return chat;
    }

    private static int NextCooldown(int current) =>
        CooldownOptions[(Array.IndexOf(CooldownOptions, current) + 1 + CooldownOptions.Length) %
                        CooldownOptions.Length];

    private static bool TryParseCategory(string? value, out BotCommandCategory category) =>
        Enum.TryParse(value, ignoreCase: true, out category) &&
        BotCommandCatalog.Categories.Contains(category);

    private async Task SynchronizeCommandsAsync(long chatId, TelegramChat settings)
    {
        BotCommandScope scope = BotCommandScope.Chat(chatId);
        if (!settings.CommandRestrictionsEnabled)
        {
            await Context.BotClient.DeleteMyCommands(scope, cancellationToken: Context.CancellationToken);
            return;
        }

        await Context.BotClient.SetMyCommands(
            BotCommandCatalog.CreateTelegramCommands(settings.DisabledCommands, ephemeral: true),
            scope,
            cancellationToken: Context.CancellationToken);
    }

    private async Task EditPanelAsync(
        Message panel,
        long receiverUserId,
        int ephemeralMessageId,
        TelegramChat settings,
        string view,
        BotCommandCategory? selectedCategory,
        ILocalization language)
    {
        string text;
        InlineKeyboardMarkup keyboard;

        if (view == "home")
        {
            text = GroupCommandLimitsCommand.BuildText(settings, language);
            keyboard = GroupCommandLimitsCommand.BuildMainKeyboard(settings, language);
        }
        else if (view == "categories")
        {
            text = $"{language.groupLimits_title}\n{language.groupLimits_categories}";
            keyboard = BuildCategoriesKeyboard(settings, language);
        }
        else
        {
            BotCommandCategory category = selectedCategory
                ?? throw new InvalidOperationException("A command category is required for the category view.");
            text = $"{language.groupLimits_title}\n{BotCommandCatalog.GetCategoryName(category, language)}";
            keyboard = BuildCommandsKeyboard(settings, category, language);
        }

        await Context.BotClient.EditEphemeralMessageText(
            panel.Chat.Id,
            receiverUserId,
            ephemeralMessageId,
            text,
            replyMarkup: keyboard,
            cancellationToken: Context.CancellationToken);
    }

    private static InlineKeyboardMarkup BuildCategoriesKeyboard(TelegramChat settings, ILocalization language)
    {
        List<InlineKeyboardButton[]> rows = [];
        foreach (BotCommandCategory category in BotCommandCatalog.Categories)
        {
            BotCommandDefinition[] commands = BotCommandCatalog.GetCategory(category).ToArray();
            int disabledCount = commands.Count(command =>
                BotCommandCatalog.IsDisabled(command.Command, settings.DisabledCommands));
            string indicator = disabledCount switch
            {
                0 => "✅",
                var count when count == commands.Length => "⛔",
                _ => "◐"
            };
            rows.Add([
                InlineKeyboardButton.WithCallbackData(
                    $"{indicator} {BotCommandCatalog.GetCategoryName(category, language)}",
                    $"{Command} category {category}")
            ]);
        }

        rows.Add([InlineKeyboardButton.WithCallbackData(language.common_back, $"{Command} home")]);
        rows.Add([InlineKeyboardButton.WithCallbackData(language.groupLimits_closed, $"{Command} close")]);
        return new InlineKeyboardMarkup(rows);
    }

    private static InlineKeyboardMarkup BuildCommandsKeyboard(
        TelegramChat settings,
        BotCommandCategory category,
        ILocalization language)
    {
        List<InlineKeyboardButton[]> rows = [];
        BotCommandDefinition[] commands = BotCommandCatalog.GetCategory(category).ToArray();
        foreach (BotCommandDefinition definition in commands)
        {
            bool disabled = BotCommandCatalog.IsDisabled(definition.Command, settings.DisabledCommands);
            string indicator = disabled ? "⛔" : "✅";
            rows.Add([
                InlineKeyboardButton.WithCallbackData(
                    $"{indicator} {definition.Command}",
                    $"{Command} command {definition.Command}")
            ]);
        }

        int disabledCount = commands.Count(command =>
            BotCommandCatalog.IsDisabled(command.Command, settings.DisabledCommands));
        rows.Insert(0,
        [
            InlineKeyboardButton.WithCallbackData(
                disabledCount == commands.Length
                    ? language.groupLimits_enableCategory
                    : language.groupLimits_disableCategory,
                $"{Command} category-toggle {category}")
        ]);

        if (rows.Count == 0)
            rows.Add([InlineKeyboardButton.WithCallbackData(language.groupLimits_noCommands, $"{Command} noop")]);

        rows.Add([InlineKeyboardButton.WithCallbackData(language.common_back, $"{Command} categories")]);
        rows.Add([InlineKeyboardButton.WithCallbackData(language.groupLimits_closed, $"{Command} close")]);
        return new InlineKeyboardMarkup(rows);
    }
}
