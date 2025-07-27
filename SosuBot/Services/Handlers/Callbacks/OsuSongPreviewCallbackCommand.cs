using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Callbacks
{
    public class OsuSongPreviewCallbackCommand : CommandBase<CallbackQuery>
    {
        public static string Command = "songpreview";

        public override async Task ExecuteAsync()
        {
            string[] parameters = Context.Update.Data!.Split(' ');
            long chatId = long.Parse(parameters[0]);
            int beatmapsetId = int.Parse(parameters[2]);

            byte[] data = await OsuHelper.GetSongPreviewAsync(beatmapsetId);
            using MemoryStream ms = new MemoryStream(data);
            await Context.BotClient.SendAudio(chatId, new InputFileStream(ms, $"{beatmapsetId}.mp3"), "Запрос от: " + TelegramHelper.GetUserUrlWrappedInString(Context.Update.From.Id, TelegramHelper.GetUserFullName(Context.Update.From)), replyParameters: Context.Update.Message!.Id, parseMode: ParseMode.Html);
        }
    }
}
