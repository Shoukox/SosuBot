using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SosuBot.Database;
using SosuBot.Database.Database.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using static SosuBot.Services.ReplayRenderService;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class PostVideoCommand : CommandBase<Message>
{
    private const int VideoWidth = 2560;
    private const int VideoHeight = 1440;

    public static readonly string[] Commands = ["/postvideo"];
    public static readonly string Description = "подготовить видео для публикации по score";
    private ReplayRenderPreparationService _preparationService = null!;
    private ReplayRenderWorkflowService _workflowService = null!;
    private ReplayRenderPresentationService _presentationService = null!;
    private PostVideoArtifactService _artifactService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;
    private ILogger<PostVideoCommand> _logger = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _preparationService = Context.ServiceProvider.GetRequiredService<ReplayRenderPreparationService>();
        _workflowService = Context.ServiceProvider.GetRequiredService<ReplayRenderWorkflowService>();
        _presentationService = Context.ServiceProvider.GetRequiredService<ReplayRenderPresentationService>();
        _artifactService = Context.ServiceProvider.GetRequiredService<PostVideoArtifactService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<PostVideoCommand>>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(
            RateLimiterFactory.RateLimitPolicy.RenderCommand);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_rateLimit);
            return;
        }

        OsuUser? osuUser = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUser is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        if (!ReplayRenderPreparationService.TryParseScoreLink(
                Context.Update,
                out long scoreId,
                out string scoreLink))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.postVideo_usage);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        ReplayRenderPreparationResult preparation = await _preparationService.PrepareScoreAsync(
            scoreId,
            scoreLink,
            osuUser.RenderSettings,
            Context.CancellationToken);
        if (preparation.Request is not { } preparedReplay)
        {
            await _presentationService.EditPreparationFailureAsync(
                Context.BotClient,
                waitMessage,
                language,
                preparation);
            return;
        }

        using (preparedReplay)
        {
            RenderSettings renderSettings = ReplayRenderPreparationService.CreateRenderSettings(
                osuUser.RenderSettings,
                preparedReplay) with
            {
                VideoWidth = VideoWidth,
                VideoHeight = VideoHeight,
                ShowPP = true,
                HitErrorMeter = true
            };
            ReplayRenderWorkflowResult render = await _workflowService.QueueAndWaitAsync(
                Context.BotClient,
                waitMessage,
                language,
                preparedReplay.ReplayStream,
                renderSettings,
                $"telegram-postvideo:{Context.Update.From!.Id}",
                preparedReplay.IsRulesetNotStd,
                Context.CancellationToken);
            if (!render.Succeeded)
                return;

            RenderJob job = render.Job!;
            string outputDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "videos",
                "postvideo",
                scoreId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(outputDirectory);

            string? sourceVideoPath = _presentationService.ResolveVideoPath(job);
            string outputVideoPath = Path.Combine(outputDirectory, "video.mp4");
            if (sourceVideoPath is not null)
            {
                await CopyFileAsync(sourceVideoPath, outputVideoPath, Context.CancellationToken);
            }

            PostVideoArtifacts? artifacts = null;
            try
            {
                artifacts = await _artifactService.GenerateAsync(
                    scoreId,
                    preparedReplay.Score!,
                    outputDirectory,
                    renderSettings.SkinName,
                    Context.CancellationToken);
            }
            catch (OperationCanceledException) when (Context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not generate postvideo artifacts for score {ScoreId}",
                    scoreId);
            }

            await _presentationService.SendVideoAsync(
                Context.BotClient,
                render.StatusMessage,
                job,
                renderSettings,
                language,
                Context.CancellationToken,
                File.Exists(outputVideoPath) ? outputVideoPath : sourceVideoPath);

            if (artifacts is not null)
                await SendArtifactsAsync(artifacts);
        }
    }

    private async Task SendArtifactsAsync(PostVideoArtifacts artifacts)
    {
        try
        {
            if (artifacts.PreviewPath is { } previewPath && File.Exists(previewPath))
            {
                await using FileStream previewStream = new(
                    previewPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                await Context.Update.ReplyPhotoAsync(
                    Context.BotClient,
                    new InputFileStream(previewStream, "preview.png"),
                    artifacts.Title.EncodeHtml());
            }

            if (File.Exists(artifacts.TitlePath))
            {
                await using FileStream titleStream = new(
                    artifacts.TitlePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                await Context.Update.ReplyDocumentAsync(
                    Context.BotClient,
                    new InputFileStream(titleStream, "title.txt"));
            }

            if (File.Exists(artifacts.DescriptionPath))
            {
                await using FileStream descriptionStream = new(
                    artifacts.DescriptionPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                await Context.Update.ReplyDocumentAsync(
                    Context.BotClient,
                    new InputFileStream(descriptionStream, "description.txt"));
            }
        }
        catch (OperationCanceledException) when (Context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
                _logger.LogWarning(
                exception,
                "Could not send postvideo artifacts from {Directory}",
                artifacts.DirectoryPath);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        await using FileStream destination = new(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        await source.CopyToAsync(destination, cancellationToken);
    }
}
