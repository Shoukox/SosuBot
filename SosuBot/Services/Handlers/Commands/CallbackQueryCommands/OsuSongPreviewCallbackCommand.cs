using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Beatmaps.Models.HttpIO;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
using System.Net.Mail;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SosuBot.Services.Handlers.MessageCommands
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
