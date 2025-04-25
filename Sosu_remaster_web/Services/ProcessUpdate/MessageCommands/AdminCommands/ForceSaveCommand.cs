using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands.AdminCommands
{
    public class ForceSaveCommand : ICommand
    {
        public const string commandText = "/forcesave";
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            if (Variables.WHITELIST.Contains(message.From.Id))
            {
                TextDatabase.SaveData();
                await bot.SendTextMessageAsync(message.Chat.Id, "saved");
            }
        });
    }
}
