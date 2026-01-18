using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class RenderSkinSetCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/setskin"];
    private ReplayRenderService _replayRenderService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        if (Context.Update.ReplyToMessage == null || Context.Update.ReplyToMessage.Document == null ||
            Context.Update.ReplyToMessage?.Document.FileName![^4..] != ".osk")
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Эту команду нужно использовать ответом на файл скина");
            return;
        }

        //if (Context.Update.ReplyToMessage?.Document.FileSize >= 20971520)
        //{
        //    await Context.Update.ReplyAsync(Context.BotClient, "Из-за ограничений телеграма размер скина должен быть меньше 20мб");
        //    return;
        //}

        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);


        // Fake 500ms wait
        await Task.Delay(500);


        Stream skinStream = new MemoryStream();
        var tgfile = await Context.BotClient.GetFile(Context.Update.ReplyToMessage!.Document.FileId);
        await Context.BotClient.DownloadFileConsideringLocalServer(tgfile, skinStream);
        skinStream.Position = 0;

        var fileName = Context.Update.ReplyToMessage!.Document.FileName!;
        fileName = Regex.Replace(fileName, @"\s+", " ");

        string asciiSkinName = AnyAscii.Transliteration.Transliterate(fileName);
        asciiSkinName = asciiSkinName.Substring(0, asciiSkinName.Length - 4);
        asciiSkinName = asciiSkinName.Substring(0, Math.Min(53, asciiSkinName.Length)) + ".osk";
        await _replayRenderService.UploadSkin(skinStream, asciiSkinName);

        await waitMessage.EditAsync(Context.BotClient, "Скин успешно загружен и будет использован по умолчанию!");
        osuUserInDatabase.RenderSettings.SkinName = asciiSkinName;
    }
}