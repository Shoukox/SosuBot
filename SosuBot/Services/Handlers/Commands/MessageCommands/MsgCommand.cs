using Microsoft.Extensions.Logging;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands.MessageCommands
{
    public class MsgCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/msg"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Text!.GetCommandParameters()!;
            if (parameters[0] == "groups")
            {
                string msg = string.Join(" ", parameters[1..]);

                foreach (var chat in Database.TelegramChats)
                {
                    try
                    {
                        await BotClient.SendMessage(chat.ChatId, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
                        await Task.Delay(500);
                    }
                    catch (ApiRequestException reqEx)
                    {
                        Logger.LogError(reqEx, $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"Exception in MsgCommand while sending message to group {chat.ChatId}");
                    }
                }
            }
            else if (parameters[0] == "user")
            {
                string id = parameters[1];
                string msg = string.Join(" ", parameters[2..]);
                await BotClient.SendMessage(id, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            await Context.ReplyAsync(BotClient, "Done.");
        }
    }
}
