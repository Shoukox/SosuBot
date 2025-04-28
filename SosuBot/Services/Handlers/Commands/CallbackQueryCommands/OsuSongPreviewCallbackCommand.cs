using Sosu.Localization;
using SosuBot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands.CallbackQueryCommands
{
    public class OsuSongPreviewCallbackCommand : CommandBase<CallbackQuery>
    {
        public static string Command = "songpreview";

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();

            string[] parameters = Context.Data!.Split(' ');
            long chatId = long.Parse(parameters[0]);
            int beatmapsetId = int.Parse(parameters[2]);

            byte[] data = await OsuHelper.GetSongPreviewAsync(beatmapsetId);
            using MemoryStream ms = new MemoryStream(data);
            await BotClient.SendAudio(chatId, new InputFileStream(ms));
        }
    }
}
