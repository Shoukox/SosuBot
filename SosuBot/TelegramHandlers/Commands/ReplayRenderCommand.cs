using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Database.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class ReplayRenderCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/render"];
    public static readonly string Description = "отрендерить osu! реплей";
    private ReplayRenderPreparationService _preparationService = null!;
    private ReplayRenderWorkflowService _workflowService = null!;
    private ReplayRenderPresentationService _presentationService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _preparationService = Context.ServiceProvider.GetRequiredService<ReplayRenderPreparationService>();
        _workflowService = Context.ServiceProvider.GetRequiredService<ReplayRenderWorkflowService>();
        _presentationService = Context.ServiceProvider.GetRequiredService<ReplayRenderPresentationService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
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

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        ReplayRenderPreparationResult preparation = await _preparationService.PrepareMessageAsync(
            Context.BotClient,
            Context.Update,
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
                preparedReplay);
            ReplayRenderWorkflowResult render = await _workflowService.QueueAndWaitAsync(
                Context.BotClient,
                waitMessage,
                language,
                preparedReplay.AutoRequested ? null : preparedReplay.ReplayStream,
                renderSettings,
                $"telegram-user:{Context.Update.From!.Id}",
                preparedReplay.IsRulesetNotStd,
                Context.CancellationToken);
            if (!render.Succeeded)
                return;

            await _presentationService.SendVideoAsync(
                Context.BotClient,
                render.StatusMessage,
                render.Job!,
                renderSettings,
                language,
                Context.CancellationToken);
        }
    }
}
