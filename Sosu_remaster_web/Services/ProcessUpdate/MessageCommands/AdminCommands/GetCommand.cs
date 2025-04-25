using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands.AdminCommands
{
    public class GetCommand : ICommand
    {
        public const string commandText = "/get";
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            if (Variables.WHITELIST.Contains(message.From.Id))
            {
                if (message.ReplyToMessage != null)
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(message.ReplyToMessage, Newtonsoft.Json.Formatting.Indented);
                    await bot.SendTextMessageAsync(message.Chat.Id, json, replyToMessageId: message.MessageId);
                }
            }
        });
    }
}
