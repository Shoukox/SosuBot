using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuSetCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            string name = string.Join(" ", message.Text.Split(" ").Skip(1));

            if (string.IsNullOrEmpty(name))
            {
                await bot.SendTextMessageAsync(message.Chat.Id, language.error_nameIsEmpty, ParseMode.Html, replyToMessageId: message.MessageId);
                return;
            }

            var item = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            if (item == default)
            {
                Variables.osuUsers.Add(new Sosu.Types.osuUser(telegramId: message.From.Id, osuName: name, pp: 0));
                item = Variables.osuUsers.Last();
                //Variables.db.InsertOrUpdateOsuUsersTable(item, true);
                Console.WriteLine("added");
            }
            else
            {
                Console.WriteLine($"changed, last = {item.osuName}, new = {name}");
                item.telegramId = message.From.Id;
                item.osuName = name;
                item.pp = 0;
                //Variables.db.InsertOrUpdateOsuUsersTable(item, false);

            }
            string sendText = Localization.Localization.Methods.ReplaceEmpty(language.command_set, new[] { $"{name}" });
            await bot.SendTextMessageAsync(message.Chat.Id, sendText, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
        });
    }
}
