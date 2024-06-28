using Sosu.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class StartCommand : ICommand
    {
        //public string pattern
        //{
        //    get => "/start";
        //}
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
            {
                Message message = update.Message;

                var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);

                ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

                string text = language.command_start;
                await bot.SendTextMessageAsync(message.Chat.Id, text);
            });

        public async Task nachAction(ITelegramBotClient bot, Message message)
        {

        }
    }
}
