using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuDeletePlayersFromChatstats : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            string[] splittedMessage = message.Text.Split(' ');

            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            List<Sosu.Types.osuUser> chatMembers = new();


            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);
            foreach (var item in chat.members)
            {
                var curUser = Variables.osuUsers.FirstOrDefault(m => m.telegramId == item);
                if (curUser != null)
                {
                    chatMembers.Add(curUser);
                }
            }

            List<string> successfullyDeleted = new List<string>();
            foreach (string playerToDelete in splittedMessage.Skip(1))
            {
                var osuUser = Variables.osuUsers.First(m => m.osuName.ToLowerInvariant() == playerToDelete.ToLowerInvariant());
                if (chat.members.Remove(osuUser.telegramId))
                {
                    successfullyDeleted.Add(playerToDelete);
                };
            }
            string sendText = Localization.Localization.Methods.ReplaceEmpty(language.command_delete_user_chatstats, successfullyDeleted);
            await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, sendText, ParseMode.Html, disableWebPagePreview: true);
        });
    }
}
