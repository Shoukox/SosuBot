using Sosu.Services.ProcessUpdate.Tools;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuUserCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
            {
                var message = update.Message;

                var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
                var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);

                ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

                Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);
                string[] splittedMessage = message.Text.Split(' ');

                ParsedProfile? parsedProfile = null;
                if (splittedMessage.Length == 1)
                {
                    if (user == null)
                    {
                        await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, language.error_noUser, ParseMode.Html);
                        return;
                    }
                    parsedProfile = new ParsedProfile(user.osuName);
                }
                else if (splittedMessage.Length >= 2)
                    parsedProfile = new ParsedProfile(string.Join(" ", splittedMessage.Skip(1)));

                if (parsedProfile == null)
                {
                    await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, language.error_userNotFound, ParseMode.Html);
                    return;
                }

                osu.V1.Types.User osuUser = await parsedProfile.Parse();

                if (splittedMessage.Length == 1)
                    user.osuName = osuUser.username();

                string textToSend = Localization.Localization.Methods.ReplaceEmpty(language.command_user, new[] { "Standard", $"{osuUser.profile_url()}", $"{osuUser.username()}", $"{osuUser.pp_rank()}", $"{osuUser.pp_country_rank()}", $"{osuUser.country()}", $"{osuUser.pp_raw():N2}", $"{parsedProfile.different:N2}", $"{double.Parse(osuUser.accuracy()):N2}", $"{osuUser.playcount()}", $"{osuUser.playtime_hours()}", $"{osuUser.count_rank_ssh()}", $"{osuUser.count_rank_sh()}", $"{osuUser.count_rank_ss()}", $"{osuUser.count_rank_s()}", $"{osuUser.count_rank_a()}" });
                var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
                    {
                            new InlineKeyboardButton[] {new InlineKeyboardButton("Standard") {CallbackData = $"{message.Chat.Id} user 0 {osuUser.username()}"}, new InlineKeyboardButton("Taiko") {CallbackData = $"{message.Chat.Id} user 1 {osuUser.username()}" }},
                            new InlineKeyboardButton[] {new InlineKeyboardButton("Catch") {CallbackData = $"{message.Chat.Id} user 2 {osuUser.username()}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{message.Chat.Id} user 3 {osuUser.username()}" }}
                    });

                await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);

                if (parsedProfile.osuUserLastVersion != null && parsedProfile.osuUserLastVersion.Count() != 0)
                {
                    foreach (var item in parsedProfile.osuUserLastVersion)
                    {
                        item.pp = double.Parse(osuUser.pp_raw());
                        //Variables.db.InsertOrUpdateOsuUsersTable(item, false);
                    }
                }
            });
    }
}
