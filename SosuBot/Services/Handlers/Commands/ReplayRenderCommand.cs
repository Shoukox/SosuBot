using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class ReplayRenderCommand : CommandBase<Message>
{
    public static string[] Commands = ["/render"];

    public override async Task ExecuteAsync()
    {
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        if (Context.Update.ReplyToMessage?.Document == null ||
            Context.Update.ReplyToMessage?.Document.FileName![^4..] != ".osr")
            return;

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var tempFilePath = Path.GetTempFileName();
        var tempFileName = Path.GetFileName(tempFilePath);
        var tgfile = await Context.BotClient.GetFile(Context.Update.ReplyToMessage.Document.FileId);

        var replayPath = $"/home/shoukko/danser/replays/{tempFileName}";
        var sw = new StreamWriter(replayPath);
        await Context.BotClient.DownloadFile(tgfile, sw.BaseStream);
        sw.Close();

        await Context.RabbitMqService.QueueJob(tempFileName);
        // var fileName = match.Groups[1].Value;
        // var sendText = $"{match.Value}\n\n" +
        //                "http://[2a03:4000:6:417a:1::105]/" + fileName;

        string mp4FileName = tempFileName[..^4] + ".mp4";
        string sendText = $"http://[2a03:4000:6:417a:1::105]/videos/{mp4FileName}";
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}