using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuChatstatsCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            List<Sosu.Types.osuUser> chatMembers = new();

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);
            string sendText = language.command_chatstats_title;
            foreach (var item in chat.members)
            {
                var curUser = Variables.osuUsers.FirstOrDefault(m => m.telegramId == item);
                if (curUser != null)
                {
                    chatMembers.Add(curUser);
                }
            }
            var sortedChatMembers = chatMembers.OrderByDescending(m => m.pp).ToList();

            int i = 1;
            foreach (var item in sortedChatMembers)
            {
                if (i == 11) break;
                sendText += Localization.Localization.Methods.ReplaceEmpty(language.command_chatstats_row, new[] { $"{i}", $"{item.osuName}", $"{item.pp: 0}" });
                i += 1;
            }

            sendText += language.command_chatstats_end;
            await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, sendText, ParseMode.Html, disableWebPagePreview: true);
        });
    }
}
