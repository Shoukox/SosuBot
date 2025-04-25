using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace Sosu.Services.ProcessUpdate.CallbacksCommands
{
    public class OsuSongPreviewCallbackCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var callback = update.CallbackQuery;
            string[] splittedCallback = callback.Data.Split(' ');
            int beatmapset_id = int.Parse(splittedCallback[2]);

            byte[] data;
            using (HttpClient hc = new HttpClient())
            {
                data = await hc.GetByteArrayAsync($"https://b.ppy.sh/preview/{beatmapset_id}.mp3");
            }
            using (MemoryStream ms = new MemoryStream(data))
            {
                await bot.SendAudioAsync(callback.Message.Chat.Id, new InputOnlineFile(ms));
            }
            await bot.AnswerCallbackQueryAsync(callback.Id);
        });
    }
}
