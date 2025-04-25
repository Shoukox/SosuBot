using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands.AdminCommands
{
    public class DeleteCommand : ICommand
    {
        public const string commandText = "/del";
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            if (Variables.WHITELIST.Contains(message.From.Id))
            {
                if (message.ReplyToMessage != null)
                {
                    await bot.DeleteMessageAsync(message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId);
                }
            }
        });
    }
}
