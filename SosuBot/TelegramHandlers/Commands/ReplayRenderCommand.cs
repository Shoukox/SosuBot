using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Net.Sockets;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class ReplayRenderCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/render"];
    private BanchoApiV2 _osuApiV2 = null!;
    private ReplayRenderService _replayRenderService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.RenderCommand);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_rateLimit);
            return;
        }

        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        // Fake 500ms wait
        await Task.Delay(500);

        OnlineRenderer[]? onlineRenderers = null;
        onlineRenderers = await _replayRenderService.GetOnlineRenderers();
        if (onlineRenderers is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_serverDown);
            return;
        }
        //catch (HttpRequestException ex) when (ex.InnerException is SocketException socketException && socketException.ErrorCode == 10061)

        int onlineRenderersCount = onlineRenderers!.Length;
        if (onlineRenderers == null || onlineRenderers.Length == 0)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_noRenderers);
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        RenderQueuedResponse? renderQueueResponse = null;
        Stream replayStream = new MemoryStream();

        if (Context.Update.ReplyToMessage?.Document != null &&
            Context.Update.ReplyToMessage?.Document.FileName![^4..] == ".osr")
        {
            var tgfile = await Context.BotClient.GetFile(Context.Update.ReplyToMessage.Document.FileId);

            await Context.BotClient.DownloadFileConsideringLocalServer(tgfile, replayStream);
            replayStream.Position = 0;
        }
        else if (Context.Update.Document != null &&
            Context.Update.Document.FileName![^4..] == ".osr")
        {
            var tgfile = await Context.BotClient.GetFile(Context.Update.Document!.FileId);

            await Context.BotClient.DownloadFileConsideringLocalServer(tgfile, replayStream);
            replayStream.Position = 0;
        }
        else if (parameters.Length > 0 && OsuHelper.ParseOsuScoreLink([parameters[0]], out var scoreId) is { } scoreLink && scoreId != null)
        {
            var score = await _osuApiV2.Scores.GetScore(scoreId.Value);
            if (score is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.ReplayScoreNotFound(language, $"{scoreLink}"));
                return;
            }
            if (!score.HasReplay!.Value)
            {
                await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.ReplayScoreHasNoReplay(language, $"{scoreLink}"));
                return;
            }

            replayStream = await _osuApiV2.Scores.DownloadScoreReplay(scoreId.Value);
        }
        else if (Context.Update.ReplyToMessage != null && OsuHelper.ParseOsuScoreLink(Context.Update.ReplyToMessage.GetAllLinks(), out scoreId) is { } scoreLinkFromReply && scoreId != null)
        {
            var score = await _osuApiV2.Scores.GetScore(scoreId.Value);
            if (score is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.ReplayScoreNotFound(language, $"{scoreLinkFromReply}"));
                return;
            }
            if (!score.HasReplay!.Value)
            {
                await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.ReplayScoreHasNoReplay(language, $"{scoreLinkFromReply}"));
                return;
            }

            replayStream = await _osuApiV2.Scores.DownloadScoreReplay(scoreId.Value);
        }
        else
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_usage);
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
            osuUserInDatabase.RenderSettings.UseExperimentalRenderer = true;
        }

        // Queue replay
        renderQueueResponse = await _replayRenderService.QueueReplay(replayStream, osuUserInDatabase.RenderSettings);
        if (renderQueueResponse is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_skinNotFound);
            return;
        }
        replayStream.Close();

        var ik = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData(language.replayRender_statusButton, $"render-status {renderQueueResponse!.JobId}")
        });
        var message = await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.ReplayOnlineQueueSearching(language, $"{onlineRenderersCount}", $"{await _replayRenderService.GetWaitqueueLength(renderQueueResponse!.JobId)}"), replyMarkup: ik);

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
                    await message.EditAsync(Context.BotClient, language.replayRender_noRenderersLeft);
                    return;
                }
                else
                {
                    if (!rendererGotThisJob)
                    {
                        await Task.Delay(3000 + Random.Shared.Next(500, 1500));
                        await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplayOnlineQueueSearchingAgain(language, $"{onlineRenderersCount}", $"{await _replayRenderService.GetWaitqueueLength(jobInfo!.JobId)}"), replyMarkup: ik);
                    }
                }
            }
            jobInfo = await _replayRenderService.GetRenderJobInfo(renderQueueResponse!.JobId);
            if (!rendererGotThisJob && jobInfo!.RenderingBy != -1)
            {
                startedWaiting = DateTime.Now;
                rendererGotThisJob = true;

                var currentRenderer = currentOnlineRenderers.First(m => m.RendererId == jobInfo.RenderingBy);
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplayRendererInProcess(language, $"{onlineRenderersCount}", currentRenderer.RendererName, currentRenderer.UsedGPU), replyMarkup: ik);
            }

            if (rendererGotThisJob && jobInfo!.RenderingBy == -1)
            {
                rendererGotThisJob = false;
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplaySearchingNewRenderer(language, $"{onlineRenderersCount}"), replyMarkup: ik);
            }

            if (rendererGotThisJob && DateTime.Now - startedWaiting >= TimeSpan.FromSeconds(timeoutSeconds))
            {
                await Task.Delay(1000 + Random.Shared.Next(500, 1500));
                await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplayTimeout(language, $"{timeoutSeconds}"), linkPreviewEnabled: true);
                return;
            }

            if (jobInfo!.IsComplete || jobInfo.IsSuccess) break;
            await Task.Delay(2000);
        }
        if (!jobInfo!.IsSuccess)
        {
            if (jobInfo.FailureReason == "ruleset")
            {
                await message.EditAsync(Context.BotClient, language.replayRender_onlyOsuStd);
                return;
            }
            else
            {
                await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplayErrorWithReason(language, jobInfo.FailureReason));
                return;
            }
        }

        string watchUrl = jobInfo!.VideoUri.Replace("/videos/", "/watch/");
        if (watchUrl.EndsWith(".mp4"))
        {
            watchUrl = watchUrl[..^4];
        }

        await message.EditAsync(Context.BotClient, LocalizationMessageHelper.ReplayFinishedWithLink(language, $"{watchUrl}"), linkPreviewEnabled: true);
    }
}



