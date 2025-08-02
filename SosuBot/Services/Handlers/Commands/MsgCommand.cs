using Microsoft.Extensions.Logging;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Commands;

public class MsgCommand : CommandBase<Message>
{
    public static string[] Commands = ["/msg"];

    public override async Task ExecuteAsync()
    {
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, "Подожди...");

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "groups")
        {
            var msg = string.Join(" ", parameters[1..]);

            foreach (var chat in Context.Database.TelegramChats)
                try
                {
                    await Task.Delay(500);
                    await Context.BotClient.SendMessage(chat.ChatId, msg);
                }
                catch (ApiRequestException reqEx)
                {
                    Context.Logger.LogError(reqEx,
                        $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                }
                catch (Exception ex)
                {
                    Context.Logger.LogError(ex,
                        $"Exception in MsgCommand while sending message to group {chat.ChatId}");
                }
        }
        else if (parameters[0] == "me")
        {
            var msg = string.Join(" ", parameters[1..]);
            await waitMessage.EditAsync(Context.BotClient, msg);
        }
        else if (parameters[0] == "check")
        {
            if (parameters[1] == "all")
            {
                var chats = 0;
                foreach (var chat in Context.Database.TelegramChats)
                    try
                    {
                        await Task.Delay(500);
                        var chatMember = await Context.BotClient.GetChatMember(chat.ChatId, Context.BotClient.BotId);
                        if (chatMember.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Member) chats += 1;
                    }
                    catch (ApiRequestException reqEx)
                    {
                        Context.Logger.LogError(reqEx,
                            $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                    }
                    catch (Exception ex)
                    {
                        await Context.Update.ReplyAsync(Context.BotClient, ex.ToString());
                    }

                await waitMessage.EditAsync(Context.BotClient,
                    $"chats: {chats}/{Context.Database.TelegramChats.Count()}");
            }
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, "Неизвестная команда");
        }
    }
}