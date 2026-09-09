using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SosuBot.Configuration;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class GetSkinCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/getskin"];
    public static readonly string Description = "скачать скин из видео рендера";

    private ReplayRenderService _replayRenderService = null!;
    private BotConfiguration _botConfig = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _botConfig = Context.ServiceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        Message? repliedMessage = Context.Update.ReplyToMessage;
        if (repliedMessage is null || repliedMessage.From?.Id != _botConfig.Id)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.getSkin_replyToRender);
            return;
        }

        string? skinFileName = repliedMessage.GetAllLinks()
            .Select(link => Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) ? uri : null)
            .Where(uri => uri is not null && IsTrustedServerUri(uri!))
            .Select(uri => ReplayRenderService.TryGetSkinFileName(uri!, out string fileName) ? fileName : null)
            .FirstOrDefault(fileName => fileName is not null);

        if (skinFileName is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.getSkin_notFound);
            return;
        }

        string skinName = DecodeSkinName(skinFileName);
        if (skinName.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.getSkin_defaultSkin);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        ReplayRenderService.SkinDownloadResult? skin = await _replayRenderService.DownloadSkin(
            skinFileName, Context.CancellationToken);
        if (skin is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.getSkin_downloadError);
            return;
        }

        await Context.BotClient.DeleteMessage(
            waitMessage.Chat.Id,
            waitMessage.MessageId,
            Context.CancellationToken);
        await using MemoryStream skinStream = new(skin.Content, writable: false);
        await Context.Update.ReplyDocumentAsync(
            Context.BotClient,
            new InputFileStream(skinStream, skin.FileName));
    }

    private bool IsTrustedServerUri(Uri uri)
    {
        Uri serverUri = _replayRenderService.ServerUri;
        return uri.Scheme.Equals(serverUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               uri.Host.Equals(serverUri.Host, StringComparison.OrdinalIgnoreCase) &&
               uri.Port == serverUri.Port;
    }

    private static string DecodeSkinName(string skinFileName)
    {
        try
        {
            return System.Text.Encoding.ASCII.GetString(Convert.FromHexString(skinFileName[..^4]));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
