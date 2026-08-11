using Microsoft.Extensions.Logging;
using SosuBot.Extensions;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class VideoPreviewCommand(
    VideoPreviewService previewService,
    RateLimiterFactory rateLimiterFactory,
    ILogger<VideoPreviewCommand> logger) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/videopreview"];
    public static readonly string Description = "превью видео для osu! score";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        long rateLimitKey = Context.Update.From?.Id ?? Context.Update.Chat.Id;
        if (!await rateLimiter.IsAllowedAsync(rateLimitKey.ToString(CultureInfo.InvariantCulture)))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        string? rawText = ExtractPreviewText(Context.Update.Text);
        if (rawText is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.videoPreview_usage);
            return;
        }

        if (!ScorePreviewBbCodeParser.TryParse(rawText, out ScorePreviewText? previewText, out _))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.videoPreview_invalidText);
            return;
        }

        IEnumerable<string> scoreLinkCandidates = GetScoreLinkCandidates(Context.Update.ReplyToMessage);
        if (OsuHelper.ParseOsuScoreLink(scoreLinkCandidates, out long? scoreId) is null || scoreId is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.videoPreview_usage);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        VideoPreviewGenerationResult result = await previewService.GenerateAsync(scoreId.Value, previewText!,
            Context.CancellationToken);
        if (result.Failure == VideoPreviewGenerationFailure.ScoreNotFound)
        {
            await waitMessage.EditAsync(Context.BotClient, language.videoPreview_scoreNotFound);
            return;
        }

        if (result.Failure != VideoPreviewGenerationFailure.None || result.Image is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.videoPreview_generationFailed);
            return;
        }

        using MemoryStream image = new(result.Image, writable: false);
        await Context.Update.ReplyPhotoAsync(Context.BotClient,
            new InputFileStream(image, $"score-{scoreId.Value}-preview.png"));

        try
        {
            await Context.BotClient.DeleteMessage(waitMessage.Chat.Id, waitMessage.MessageId,
                Context.CancellationToken);
        }
        catch (ApiRequestException exception)
        {
            logger.LogDebug(exception, "Could not delete the /videopreview progress message");
        }
    }

    private static string? ExtractPreviewText(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return null;

        int separator = messageText.IndexOfAny([' ', '\t', '\r', '\n']);
        if (separator < 0)
            return null;

        string text = messageText[(separator + 1)..].ReplaceLineEndings(" ").Trim();
        return text.Length == 0 ? null : text;
    }

    private static IEnumerable<string> GetScoreLinkCandidates(Message? repliedMessage)
    {
        if (repliedMessage is null)
            return [];

        List<string> candidates = repliedMessage.GetAllLinks().ToList();
        if (!string.IsNullOrWhiteSpace(repliedMessage.Text))
            candidates.Add(repliedMessage.Text);
        if (!string.IsNullOrWhiteSpace(repliedMessage.Caption))
            candidates.Add(repliedMessage.Caption);
        return candidates;
    }
}
