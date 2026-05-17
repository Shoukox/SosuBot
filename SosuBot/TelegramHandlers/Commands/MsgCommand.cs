using Markdig.Renderers.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class MsgCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/msg"];
    private BotContext _database = null!;
    private ILogger<MsgCommand> _logger = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<MsgCommand>>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "chats")
        {
            var msg = string.Join(" ", parameters[1..]);

            foreach (var chat in _database.TelegramChats.ToList())
                try
                {
                    await Task.Delay(500);
                    await Context.BotClient.SendMessage(chat.ChatId, msg);
                }
                catch (ApiRequestException reqEx)
                {
                    _logger.LogError(reqEx,
                        $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Exception in MsgCommand while sending message to group {chat.ChatId}");
                }
        }
        else if (parameters[0] == "all_except_uzosu_check")
        {
            string msg = string.Join(" ", parameters[1..]);
            OsuUser adminUser = _database.OsuUsers.First(m => m.IsAdmin); // Shoukko
            TelegramChat exceptChat = _database.TelegramChats.First(m => m.ChatId == -1002693455476); // uzosu

            List<TelegramChat> allChats = _database.TelegramChats.ToList();
            List<TelegramChat> filteredChats = allChats.Where(chat =>
            {
                // Skip the exceptChat
                if (chat.ChatId == exceptChat.ChatId) return false;

                // Skip if the chat is a user in the exceptChat
                if (exceptChat.ChatMembers?.Contains(chat.ChatId) == true) return false;

                // Skip if the chat contains me
                if (chat.ChatMembers?.Contains(adminUser.TelegramId) == true) return false;

                return true;
            }).ToList();

            await Context.BotClient.SendMessage(adminUser.TelegramId, msg, ParseMode.Html);
            await Context.BotClient.SendMessage(adminUser.TelegramId, $"Отправка будет в {filteredChats.Count}/{allChats.Count} чатов. Конец.", ParseMode.Html);
        }
        else if (parameters[0] == "all_except_uzosu")
        {
            string msg = string.Join(" ", parameters[1..]);
            OsuUser adminUser = _database.OsuUsers.First(m => m.IsAdmin); // Shoukko
            TelegramChat exceptChat = _database.TelegramChats.First(m => m.ChatId == -1002693455476); // uzosu

            List<TelegramChat> allChats = _database.TelegramChats.ToList();
            List<TelegramChat> filteredChats = allChats.Where(chat =>
            {
                // Skip the exceptChat
                if (chat.ChatId == exceptChat.ChatId) return false;

                // Skip if the chat is a user in the exceptChat
                if (exceptChat.ChatMembers?.Contains(chat.ChatId) == true) return false;

                // Skip if the chat contains me
                if (chat.ChatMembers?.Contains(adminUser.TelegramId) == true) return false;

                return true;
            }).ToList();

            await Context.BotClient.SendMessage(adminUser.TelegramId, msg, ParseMode.Html);
            await Context.BotClient.SendMessage(adminUser.TelegramId, $"Начинаем отправку в {filteredChats.Count}/{allChats.Count} чатов", ParseMode.Html);

            int sent = 0;
            foreach (var chat in filteredChats)
                try
                {
                    await Task.Delay(500);
                    await Context.BotClient.SendMessage(chat.ChatId, msg, ParseMode.Html);
                    sent += 1;
                }
                catch (ApiRequestException reqEx)
                {
                    _logger.LogError(reqEx,
                        $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Exception in MsgCommand while sending message to group {chat.ChatId}");
                }

            await Context.BotClient.SendMessage(adminUser.TelegramId, $"Завершено. Успешно отправлено в {sent}/{filteredChats.Count} чатов", ParseMode.Html);
        }
        else if (parameters[0] == "me")
        {
            var msg = string.Join(" ", parameters[1..]);
            await Context.Update.ReplyAsync(Context.BotClient, msg);
        }
        else if (parameters[0] == "to")
        {
            var chatId = long.Parse(parameters[1]);
            int? messageId = parameters[2] == "null" ? null : int.Parse(parameters[2]);
            var msg = string.Join(" ", parameters[3..]);

            await Context.BotClient.SendMessage(chatId, msg, ParseMode.Html, messageId, linkPreviewOptions: false);
        }
        else if (parameters[0] == "check")
        {
            if (parameters[1] == "all")
            {
                var chats = 0;
                foreach (var chat in _database.TelegramChats)
                    try
                    {
                        await Task.Delay(500);
                        var chatMember = await Context.BotClient.GetChatMember(chat.ChatId, Context.BotClient.BotId);
                        if (chatMember.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Member) chats += 1;
                    }
                    catch (ApiRequestException reqEx)
                    {
                        _logger.LogError(reqEx,
                            $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                    }
                    catch (Exception ex)
                    {
                        await Context.Update.ReplyAsync(Context.BotClient, ex.ToString());
                    }

                await Context.Update.ReplyAsync(Context.BotClient,
                    LocalizationMessageHelper.AdminChatsSummary(language, $"{chats}", $"{_database.TelegramChats.Count()}"));
            }
        }
        else
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.admin_unknownCommand);
        }
    }
}

