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

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);

            List<string> successfullyDeleted = new List<string>();
            foreach (string playerToDelete in splittedMessage.Skip(1))
            {
                var osuUser = Variables.osuUsers.FirstOrDefault(m => 
                            m.osuName.Trim().ToLower() == playerToDelete.Trim().ToLower()
                            && chat.members.Contains(m.telegramId));

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
