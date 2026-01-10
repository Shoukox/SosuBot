using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Callbacks;

public class OsuSongPreviewCallback : CommandBase<CallbackQuery>
{
    public static readonly string Command = "songpreview";

    public override async Task ExecuteAsync()
    {
        var parameters = Context.Update.Data!.Split(' ');
        var beatmapsetId = int.Parse(parameters[1]);

        var chatId = Context.Update.Message!.Chat.Id;

        var data = await OsuHelper.GetSongPreviewAsync(beatmapsetId);
        if (data == null)
        {
            await Context.Update.AnswerAsync(Context.BotClient, text: "Song preview was not found");
            return;
        }

        using var ms = new MemoryStream(data);
        await Context.BotClient.SendAudio(chatId, new InputFileStream(ms, $"{beatmapsetId}.mp3"),
            "Запрос от: " + TelegramHelper.GetUserUrlWrappedInString(Context.Update.From.Id,
                TelegramHelper.GetUserFullName(Context.Update.From)), replyParameters: Context.Update.Message!.Id,
            parseMode: ParseMode.Html);

        await Context.Update.AnswerAsync(Context.BotClient);
    }
}