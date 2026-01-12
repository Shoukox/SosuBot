using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Serializers;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using System.Net.Sockets;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.Services.Handlers.Commands;

public sealed class ReplayRenderCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/render"];
    private ApiV2 _osuApiV2 = null!;
    private ReplayRenderService _replayRenderService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.RenderCommand);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро! Разрешено максимум 10 запросов за 1 час.");
            return;
        }

        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        // Fake 500ms wait
        await Task.Delay(500);

        OnlineRenderer[]? onlineRenderers = null;
        try
        {
            onlineRenderers = await _replayRenderService.GetOnlineRenderers();
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketException && socketException.ErrorCode == 10061)
        {
            await waitMessage.EditAsync(Context.BotClient, "Кажется, сервер сейчас не запущен. Попробуй в другой раз");
            return;
        }

        int onlineRenderersCount = onlineRenderers!.Length;
        if (onlineRenderers == null || onlineRenderers.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, "Нету ни одного доступного рендерера для рендера реплеев. Попробуй в другой раз");
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        RenderQueuedResponse? renderQueueResponse = null;
        Stream replayStream = new MemoryStream();
        if (Context.Update.ReplyToMessage?.Document != null &&
            Context.Update.ReplyToMessage?.Document.FileName![^4..] == ".osr")
        {
            var tgfile = await Context.BotClient.GetFile(Context.Update.ReplyToMessage.Document.FileId);
            if (tgfile.FileSize > 300 * 1024)
            {
                await waitMessage.EditAsync(Context.BotClient, "Этот реплей файл очень большой :(");
                return;
            }

            await Context.BotClient.DownloadFile(tgfile, replayStream);
            replayStream.Position = 0;
        }
        else if (Context.Update.Document != null &&
            Context.Update.Document.FileName![^4..] == ".osr")
        {
            var tgfile = await Context.BotClient.GetFile(Context.Update.Document!.FileId);
            if (tgfile.FileSize > 300 * 1024)
            {
                await waitMessage.EditAsync(Context.BotClient, "Этот реплей файл очень большой :(");
                return;
            }

            await Context.BotClient.DownloadFile(tgfile, replayStream);
            replayStream.Position = 0;
        }
        else if (parameters.Length > 0 && OsuHelper.ParseOsuScoreLink([parameters[0]], out var scoreId) is { } scoreLink && scoreId != null)
        {
            var score = await _osuApiV2.Scores.GetScore(scoreId.Value);
            if (score is null)
            {
                await waitMessage.EditAsync(Context.BotClient, $"<a href=\"{scoreLink}\">Скор</a> не найден");
                return;
            }
            if (!score.HasReplay!.Value)
            {
                await waitMessage.EditAsync(Context.BotClient, $"<a href=\"{scoreLink}\">Скор</a> не имеет реплея");
                return;
            }

            replayStream = await _osuApiV2.Scores.DownloadScoreReplay(scoreId.Value);
        }
        else if (Context.Update.ReplyToMessage != null && OsuHelper.ParseOsuScoreLink(Context.Update.ReplyToMessage.GetAllLinks(), out scoreId) is { } scoreLinkFromReply && scoreId != null)
        {
            var score = await _osuApiV2.Scores.GetScore(scoreId.Value);
            if (score is null)
            {
                await waitMessage.EditAsync(Context.BotClient, $"<a href=\"{scoreLinkFromReply}\">Скор</a> не найден");
                return;
            }
            if (!score.HasReplay!.Value)
            {
                await waitMessage.EditAsync(Context.BotClient, $"<a href=\"{scoreLinkFromReply}\">Скор</a> не имеет реплея");
                return;
            }

            replayStream = await _osuApiV2.Scores.DownloadScoreReplay(scoreId.Value);
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, "Используй эту команду на реплей файл или на скор с реплеем.\nЛибо укажи ссылку на скор после команды.");
            return;
        }

        // Check replay ruleset
        using var copyStream = new MemoryStream();
        replayStream.CopyTo(copyStream);
        copyStream.Position = 0;
        replayStream.Position = 0;

        var replayInfo = OsuParsers.Decoders.ReplayDecoder.Decode(copyStream);
        if (replayInfo.Ruleset != OsuParsers.Enums.Ruleset.Standard)
        {
            await waitMessage.EditAsync(Context.BotClient, "Рендер доступен только для osu!std");
            return;
        }

        // Queue replay
        renderQueueResponse = await _replayRenderService.QueueReplay(replayStream, osuUserInDatabase.RenderSettings);
        replayStream.Close();

        var ik = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("Статус", $"render-status {renderQueueResponse!.JobId}")
        });
        await waitMessage.EditAsync(Context.BotClient, $"Текущее количество онлайн рендереров: {onlineRenderersCount}\n\nОчередь: {await _replayRenderService.GetWaitqueueLength(renderQueueResponse!.JobId)}\nИщем свободный рендерер...", replyMarkup: ik);

        int timeoutSeconds = 600;
        DateTime startedWaiting = DateTime.Now;
        RenderJob? jobInfo = null;

        bool rendererGotThisJob = false;
        while (!Context.CancellationToken.IsCancellationRequested)
        {
            var currentOnlineRenderers = await _replayRenderService.GetOnlineRenderers();
            if (onlineRenderersCount != currentOnlineRenderers!.Length)
            {
                onlineRenderersCount = currentOnlineRenderers!.Length;
                if (onlineRenderersCount == 0)
                {
                    await waitMessage.EditAsync(Context.BotClient, $"Сейчас свободных рендереров не осталось, попробуй позже :(");
                    return;
                }
                else
                {
                    await Task.Delay(3000 + Random.Shared.Next(500, 1500));
                    await waitMessage.EditAsync(Context.BotClient, $"Текущее количество онлайн рендереров: {onlineRenderersCount}\n\nОчередь: {await _replayRenderService.GetWaitqueueLength(jobInfo!.JobId)}\nИщем свободный рендерер...", replyMarkup: ik);
                }
            }
            jobInfo = await _replayRenderService.GetRenderJobInfo(renderQueueResponse!.JobId);
            if (!rendererGotThisJob && jobInfo!.RenderingBy != -1)
            {
                startedWaiting = DateTime.Now;
                rendererGotThisJob = true;

                var currentRenderer = currentOnlineRenderers.First(m => m.RendererId == jobInfo.RenderingBy);
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await waitMessage.EditAsync(Context.BotClient, $"Текущее количество онлайн рендереров: {onlineRenderersCount}\n\n<b>Рендерер:</b> {currentRenderer.RendererName}\n<b>Видеокарта</b>: {currentRenderer.UsedGPU}\nРендер в процессе...", replyMarkup: ik);
            }

            if (rendererGotThisJob && jobInfo!.RenderingBy == -1)
            {
                rendererGotThisJob = false;
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await waitMessage.EditAsync(Context.BotClient, $"Текущее количество онлайн рендереров: {onlineRenderersCount}\n\nИщем нового рендерера...", replyMarkup: ik);
            }

            if (rendererGotThisJob && DateTime.Now - startedWaiting >= TimeSpan.FromSeconds(timeoutSeconds))
            {
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await waitMessage.EditAsync(Context.BotClient, $"Таймаут. Рендеринг не был завершен за {timeoutSeconds} секунд, повторите попытку.", linkPreviewEnabled: true);
                return;
            }

            if (jobInfo!.IsComplete || jobInfo.IsSuccess) break;
            await Task.Delay(2000);
        }
        if (!jobInfo!.IsSuccess)
        {
            if (jobInfo.FailureReason == "ruleset")
            {
                await waitMessage.EditAsync(Context.BotClient, "Рендер доступен только для osu!std");
                return;
            }
            else
            {
                await waitMessage.EditAsync(Context.BotClient, $"Ошибка рендера.\n{jobInfo.FailureReason}");
                return;
            }
        }
        await waitMessage.EditAsync(Context.BotClient, $"Рендер завершен.\n<a href=\"{jobInfo!.VideoUri}\">Ссылка на видео</a>", linkPreviewEnabled: true);
    }
}