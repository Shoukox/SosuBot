using Microsoft.Extensions.Logging;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class MsgCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/msg"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Update.Text!.GetCommandParameters()!;
            if (parameters[0] == "groups")
            {
                string msg = string.Join(" ", parameters[1..]);

                foreach (var chat in Context.Database.TelegramChats)
                {
                    try
                    {
                        await Context.BotClient.SendMessage(chat.ChatId, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
                        await Task.Delay(500);
                    }
                    catch (ApiRequestException reqEx)
                    {
                        Context.Logger.LogError(reqEx, $"ApiRequestException in MsgCommand while sending message to group {chat.ChatId}");
                    }
                    catch (Exception ex)
                    {
                        Context.Logger.LogError(ex, $"Exception in MsgCommand while sending message to group {chat.ChatId}");
                    }
                }
            }
            else if (parameters[0] == "user")
            {
                string id = parameters[1];
                string msg = string.Join(" ", parameters[2..]);
                await Context.BotClient.SendMessage(id, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            await Context.Update.ReplyAsync(Context.BotClient, "Done.");
        }
    }
}
