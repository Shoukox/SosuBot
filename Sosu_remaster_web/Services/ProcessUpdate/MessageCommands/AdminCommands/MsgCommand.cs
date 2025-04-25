using Sosu.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands.AdminCommands
{
    public class MsgCommand : ICommand
    {
        //public string pattern
        //{
        //    get => "/start";
        //}
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
            {
                Message message = update.Message;
                if (!Variables.WHITELIST.Contains(message.From.Id)) return;

                var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
                ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

                string[] splittedText = message.Text.Split(" ");
                if (splittedText[1] == "groups")
                {
                    string msg = string.Join(" ", splittedText[2..]);

                    _ = Task.Run(async () =>
                    {
                        foreach (var chat in Variables.chats)
                        {
                            await bot.SendTextMessageAsync(chat.chat.Id, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
                            await Task.Delay(500);
                        }
                    });
                }
                else if (splittedText[1] == "user")
                {
                    string id = splittedText[2];
                    string msg = string.Join(" ", splittedText[3..]);
                    await bot.SendTextMessageAsync(id, msg, Telegram.Bot.Types.Enums.ParseMode.Html);
                }
            });

        public async Task nachAction(ITelegramBotClient bot, Message message)
        {

        }
    }
}
