using SosuBot.Helpers.OutputText;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Callbacks;

public class OsuSongPreviewCallbackCommand : CommandBase<CallbackQuery>
{
    public static readonly string Command = "songpreview";

    public override async Task ExecuteAsync()
    {
        var parameters = Context.Update.Data!.Split(' ');
        var chatId = long.Parse(parameters[0]);
        var beatmapsetId = int.Parse(parameters[2]);

        var data = await OsuHelper.GetSongPreviewAsync(beatmapsetId);
        using var ms = new MemoryStream(data);
        await Context.BotClient.SendAudio(chatId, new InputFileStream(ms, $"{beatmapsetId}.mp3"),
            "Запрос от: " + TelegramHelper.GetUserUrlWrappedInString(Context.Update.From.Id,
                TelegramHelper.GetUserFullName(Context.Update.From)), replyParameters: Context.Update.Message!.Id,
            parseMode: ParseMode.Html);
    }
}