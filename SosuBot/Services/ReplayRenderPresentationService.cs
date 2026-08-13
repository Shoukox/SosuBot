using Microsoft.Extensions.Logging;
using SosuBot.Database.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.Services;

public sealed class ReplayRenderPresentationService(
    ILogger<ReplayRenderPresentationService> logger)
{
    public async Task<bool> EditPreparationFailureAsync(
        ITelegramBotClient botClient,
        Message message,
        ILocalization language,
        ReplayRenderPreparationResult result)
    {
        string? text = result.Failure switch
        {
            ReplayRenderPreparationFailure.Usage => language.replayRender_usage,
            ReplayRenderPreparationFailure.ApiUnavailable => language.replayRender_serverDown,
            ReplayRenderPreparationFailure.ScoreNotFound =>
                LocalizationMessageHelper.ReplayScoreNotFound(language, result.ScoreLink ?? string.Empty),
            ReplayRenderPreparationFailure.ScoreHasNoReplay =>
                LocalizationMessageHelper.ReplayScoreHasNoReplay(language, result.ScoreLink ?? string.Empty),
            ReplayRenderPreparationFailure.InvalidReplay => language.replayRender_invalidReplay,
            ReplayRenderPreparationFailure.BeatmapNotFound => language.replayRender_beatmapNotFound,
            ReplayRenderPreparationFailure.BeatmapTooLong => language.replayRender_beatmapLengthTooLong,
            _ => null
        };
        if (text is null)
            return false;

        await message.EditAsync(botClient, text);
        return true;
    }

    public async Task SendVideoAsync(
        ITelegramBotClient botClient,
        Message statusMessage,
        RenderJob job,
        RenderSettings renderSettings,
        ILocalization language,
        CancellationToken cancellationToken = default,
        string? videoPath = null)
    {
        string watchUrl = BuildWatchUrl(job.VideoUri);
        string textOrCaption = LocalizationMessageHelper.ReplayFinishedWithLink(language, watchUrl);
        videoPath ??= ResolveVideoPath(job);
        if (videoPath is null)
        {
            await statusMessage.EditAsync(botClient, textOrCaption, linkPreviewEnabled: true);
            return;
        }

        try
        {
            await using FileStream videoStream = new(
                videoPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            InputMediaVideo video = new(new InputFileStream(videoStream, "video.mp4"))
            {
                Caption = textOrCaption,
                ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html,
                SupportsStreaming = true,
                Width = renderSettings.VideoWidth,
                Height = renderSettings.VideoHeight,
                Duration = job.VideoDuration
            };
            if (!string.IsNullOrWhiteSpace(job.VideoThumbnailUri))
            {
                string thumbnailUri = job.VideoThumbnailUri.Replace("http://", "https://");
                video.Thumbnail = new InputFileUrl(thumbnailUri);
                video.Cover = new InputFileUrl(thumbnailUri);
            }

            await botClient.EditMessageMedia(statusMessage.Chat.Id, statusMessage.Id, video);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not send rendered video for job {JobId}", job.JobId);
            await statusMessage.EditAsync(botClient, textOrCaption, linkPreviewEnabled: true);
        }
    }

    public string? ResolveVideoPath(RenderJob job)
    {
        if (!string.IsNullOrWhiteSpace(job.VideoLocalPath) && File.Exists(job.VideoLocalPath))
            return job.VideoLocalPath;

        string? fileName = null;
        if (Uri.TryCreate(job.VideoUri, UriKind.Absolute, out Uri? videoUri))
            fileName = Path.GetFileName(videoUri.LocalPath);
        else if (!string.IsNullOrWhiteSpace(job.VideoUri))
            fileName = Path.GetFileName(job.VideoUri.Split('?', 2)[0]);

        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "videos", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "videos", fileName)
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    public static string BuildWatchUrl(string videoUri)
    {
        if (string.IsNullOrWhiteSpace(videoUri))
            return videoUri;

        string watchUrl = videoUri.Replace("/videos/", "/watch/");
        return watchUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            ? watchUrl[..^4]
            : watchUrl;
    }
}
