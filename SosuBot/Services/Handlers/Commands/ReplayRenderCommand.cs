using Microsoft.Extensions.DependencyInjection;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Data;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class ReplayRenderCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/render"];
    private RabbitMqService _rabbitMqService = null!;

    public override Task BeforeExecuteAsync()
    {
        _rabbitMqService = Context.ServiceProvider.GetRequiredService<RabbitMqService>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();
        
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

        var replayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "danser",
            "replays", tempFileName);
        var sw = new StreamWriter(replayPath);
        await Context.BotClient.DownloadFile(tgfile, sw.BaseStream);
        sw.Close();

        await _rabbitMqService.QueueJob(tempFileName);

        var mp4FileName = tempFileName[..^4] + ".mp4";
        var sendText = $"http://[2a03:4000:6:417a:1::105]/videos/{mp4FileName}";
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}