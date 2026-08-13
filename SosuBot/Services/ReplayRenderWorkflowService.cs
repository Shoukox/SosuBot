using System.Globalization;
using SosuBot.Database.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.Services;

/// <summary>
/// Queues a render request and owns the common renderer polling/progress flow.
/// </summary>
public sealed class ReplayRenderWorkflowService(ReplayRenderService replayRenderService)
{
    public const int TimeoutSeconds = 600;

    public async Task<ReplayRenderWorkflowResult> QueueAndWaitAsync(
        ITelegramBotClient botClient,
        Message waitMessage,
        ILocalization language,
        Stream? replayStream,
        RenderSettings renderSettings,
        string requestedBy,
        bool isRulesetNotStd,
        CancellationToken cancellationToken = default)
    {
        OnlineRenderer[]? onlineRenderers = await replayRenderService.GetOnlineRenderers();
        if (onlineRenderers is null)
        {
            Message message = await waitMessage.EditAsync(botClient, language.replayRender_serverDown);
            return new(message, null, ReplayRenderWorkflowFailure.ServerUnavailable);
        }

        int onlineRenderersCount = onlineRenderers.Length;
        if (onlineRenderersCount == 0)
        {
            Message message = await waitMessage.EditAsync(botClient, language.replayRender_noRenderers);
            return new(message, null, ReplayRenderWorkflowFailure.NoRenderers);
        }

        RenderQueuedResponse? queued = await replayRenderService.QueueReplay(
            replayStream,
            renderSettings,
            requestedBy);
        if (queued is null)
        {
            Message message = await waitMessage.EditAsync(botClient, language.replayRender_skinNotFound);
            return new(message, null, ReplayRenderWorkflowFailure.QueueFailed);
        }

        InlineKeyboardMarkup keyboard = new(
        [
            [InlineKeyboardButton.WithCallbackData(language.replayRender_statusButton, $"render-status {queued.JobId}"),
                InlineKeyboardButton.WithCallbackData(language.replayRender_cancelButton, $"render-cancel {queued.JobId}")]
        ]);
        Message statusMessage = await waitMessage.EditAsync(
            botClient,
            LocalizationMessageHelper.ReplayOnlineQueueSearching(
                language,
                onlineRenderersCount.ToString(CultureInfo.InvariantCulture),
                (await replayRenderService.GetWaitqueueLength(queued.JobId))
                    .ToString(CultureInfo.InvariantCulture)),
            replyMarkup: keyboard);

        return await WaitForCompletionAsync(
            botClient,
            statusMessage,
            language,
            queued.JobId,
            onlineRenderersCount,
            isRulesetNotStd,
            keyboard,
            cancellationToken);
    }

    private async Task<ReplayRenderWorkflowResult> WaitForCompletionAsync(
        ITelegramBotClient botClient,
        Message statusMessage,
        ILocalization language,
        int jobId,
        int onlineRenderersCount,
        bool isRulesetNotStd,
        InlineKeyboardMarkup keyboard,
        CancellationToken cancellationToken)
    {
        bool rendererGotThisJob = false;
        DateTime queuedAt = DateTime.UtcNow;
        DateTime renderingStartedAt = queuedAt;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (replayRenderService.IsRenderCancelled(jobId))
                {
                    Message message = await statusMessage.EditAsync(botClient, language.replayRender_cancelled);
                    return new(message, null, ReplayRenderWorkflowFailure.Cancelled);
                }

                OnlineRenderer[]? currentOnlineRenderers =
                    await replayRenderService.GetOnlineRenderers();
                if (currentOnlineRenderers is null || currentOnlineRenderers.Length != onlineRenderersCount)
                {
                    onlineRenderersCount = currentOnlineRenderers?.Length ?? 0;
                    if (onlineRenderersCount == 0)
                    {
                        Message message = await statusMessage.EditAsync(
                            botClient,
                            language.replayRender_noRenderersLeft);
                        return new(message, null, ReplayRenderWorkflowFailure.NoRenderers);
                    }

                    if (!rendererGotThisJob)
                    {
                        await Task.Delay(3000 + Random.Shared.Next(500, 1500), cancellationToken);
                        statusMessage = await statusMessage.EditAsync(
                            botClient,
                            LocalizationMessageHelper.ReplayOnlineQueueSearchingAgain(
                                language,
                                onlineRenderersCount.ToString(CultureInfo.InvariantCulture),
                                (await replayRenderService.GetWaitqueueLength(jobId))
                                    .ToString(CultureInfo.InvariantCulture)),
                            replyMarkup: keyboard);
                    }
                }

                RenderJob? job = await replayRenderService.GetRenderJobInfo(jobId);
                if (job is null)
                {
                    if (DateTime.UtcNow - queuedAt >= TimeSpan.FromSeconds(TimeoutSeconds))
                    {
                        Message message = await EditTimeoutAsync(
                            botClient,
                            statusMessage,
                            language,
                            cancellationToken);
                        return new(message, null, ReplayRenderWorkflowFailure.Timeout);
                    }

                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                if (!rendererGotThisJob && job.RenderingBy != -1)
                {
                    rendererGotThisJob = true;
                    renderingStartedAt = DateTime.UtcNow;
                    OnlineRenderer renderer = currentOnlineRenderers?.FirstOrDefault(
                        current => current.RendererId == job.RenderingBy)
                        ?? new OnlineRenderer
                        {
                            RendererName = $"#{job.RenderingBy}",
                            UsedGPU = "unknown"
                        };
                    string helpText = isRulesetNotStd
                        ? "\n\n" + language.replayRender_usingExperimentalRenderer
                        : string.Empty;
                    statusMessage = await statusMessage.EditAsync(
                        botClient,
                        LocalizationMessageHelper.ReplayRendererInProcess(
                            language,
                            onlineRenderersCount.ToString(CultureInfo.InvariantCulture),
                            renderer.RendererName,
                            renderer.UsedGPU) + helpText,
                        replyMarkup: keyboard);
                }

                if (rendererGotThisJob && job.RenderingBy == -1 && !job.IsComplete)
                {
                    rendererGotThisJob = false;
                    statusMessage = await statusMessage.EditAsync(
                        botClient,
                        LocalizationMessageHelper.ReplaySearchingNewRenderer(
                            language,
                            onlineRenderersCount.ToString(CultureInfo.InvariantCulture)) +
                        (isRulesetNotStd ? "\n\n" + language.replayRender_usingExperimentalRenderer : string.Empty),
                        replyMarkup: keyboard);
                }

                DateTime timeoutStartedAt = rendererGotThisJob ? renderingStartedAt : queuedAt;
                if (DateTime.UtcNow - timeoutStartedAt >= TimeSpan.FromSeconds(TimeoutSeconds))
                {
                    Message message = await EditTimeoutAsync(
                        botClient,
                        statusMessage,
                        language,
                        cancellationToken);
                    return new(message, null, ReplayRenderWorkflowFailure.Timeout);
                }

                if ((job.IsComplete || job.IsSuccess) && job.FailureReason != "Cancelled")
                    return await FinishJobAsync(botClient, statusMessage, language, job);
                if (job.IsComplete && job.FailureReason == "Cancelled")
                {
                    Message message = await statusMessage.EditAsync(botClient, language.replayRender_cancelled);
                    return new(message, job, ReplayRenderWorkflowFailure.Cancelled);
                }

                await Task.Delay(2000, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new(statusMessage, null, ReplayRenderWorkflowFailure.Cancelled);
        }
        finally
        {
            replayRenderService.ClearCancelledRender(jobId);
        }
    }

    private static async Task<ReplayRenderWorkflowResult> FinishJobAsync(
        ITelegramBotClient botClient,
        Message statusMessage,
        ILocalization language,
        RenderJob job)
    {
        if (job.IsSuccess)
            return new(statusMessage, job, ReplayRenderWorkflowFailure.None);

        string failureText = job.FailureReason switch
        {
            "ruleset" => language.replayRender_onlyOsuStd,
            "beatmap_not_found" => language.replayRender_beatmapNotFound,
            _ => LocalizationMessageHelper.ReplayErrorWithReason(language, job.FailureReason)
        };
        Message message = await statusMessage.EditAsync(botClient, failureText);
        return new(message, job, ReplayRenderWorkflowFailure.RenderFailed);
    }

    private static async Task<Message> EditTimeoutAsync(
        ITelegramBotClient botClient,
        Message statusMessage,
        ILocalization language,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await statusMessage.EditAsync(
            botClient,
            LocalizationMessageHelper.ReplayTimeout(
                language,
                TimeoutSeconds.ToString(CultureInfo.InvariantCulture)),
            linkPreviewEnabled: true);
    }
}

public enum ReplayRenderWorkflowFailure
{
    None,
    ServerUnavailable,
    NoRenderers,
    QueueFailed,
    Cancelled,
    Timeout,
    RenderFailed
}

public sealed record ReplayRenderWorkflowResult(
    Message StatusMessage,
    ReplayRenderService.RenderJob? Job,
    ReplayRenderWorkflowFailure Failure)
{
    public bool Succeeded => Failure == ReplayRenderWorkflowFailure.None && Job?.IsSuccess == true;
}
