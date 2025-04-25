using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class HelpCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            await bot.SendTextMessageAsync(message.Chat.Id, language.command_help, ParseMode.Html);
        });
    }
}
