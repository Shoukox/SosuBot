using Sosu.osu.V1.Enums;
using Sosu.Services.ProcessUpdate.Tools;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.CallbacksCommands
{
    public class OsuUserCallbackCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var callback = update.CallbackQuery;
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == callback.Message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            string name = "";
            string[] splittedCallback = callback.Data.Split(' ');
            int mode = int.Parse(splittedCallback[2]);

            for (int i = 3; i <= splittedCallback.Length - 1; i++)
            {
                name += splittedCallback[i];
                if (i != splittedCallback.Length - 1) name += " ";
            }
            ParsedProfile parsedProfile = new ParsedProfile(name);
            Sosu.osu.V1.Types.User osuUser = await parsedProfile.Parse((GameMode)mode);

            string different = parsedProfile.different;
            string textToSend = Localization.Localization.Methods.ReplaceEmpty(language.command_user, new[] { $"{Enum.GetName(typeof(GameMode), mode)}", $"{osuUser.profile_url()}", $"{osuUser.username()}", $"{osuUser.pp_rank()}", $"{osuUser.pp_country_rank()}", $"{osuUser.country()}", $"{osuUser.pp_raw():N2}", $"{different:N2}", $"{double.Parse(osuUser.accuracy()):N2}", $"{osuUser.playcount()}", $"{osuUser.playtime_hours()}", $"{osuUser.count_rank_ssh()}", $"{osuUser.count_rank_sh()}", $"{osuUser.count_rank_ss()}", $"{osuUser.count_rank_s()}", $"{osuUser.count_rank_a()}" });
            var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
                {
                    new InlineKeyboardButton[] {new InlineKeyboardButton("Standard") {CallbackData = $"{callback.Message.Chat.Id} user 0 {name}"}, new InlineKeyboardButton("Taiko") {CallbackData = $"{callback.Message.Chat.Id} user 1 {name}" }},
                    new InlineKeyboardButton[] {new InlineKeyboardButton("Catch") {CallbackData = $"{callback.Message.Chat.Id} user 2 {name}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{callback.Message.Chat.Id} user 3 {name}" }}

                });
            await bot.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);
            await bot.AnswerCallbackQueryAsync(callback.Id);

        });
    }
}
