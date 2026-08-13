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

public sealed class ReplayRenderCommand(
    ReplayRenderPreparationService preparationService,
    ReplayRenderWorkflowService workflowService,
    ReplayRenderPresentationService presentationService,
    RateLimiterFactory rateLimiterFactory,
    BotContext database) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/render"];
    public static readonly string Description = "отрендерить osu! реплей";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = rateLimiterFactory.Get(
            RateLimiterFactory.RateLimitPolicy.RenderCommand);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.replayRender_rateLimit);
            return;
        }

        OsuUser? osuUser = await database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUser is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        ReplayRenderPreparationResult preparation = await preparationService.PrepareMessageAsync(
            Context.BotClient,
            Context.Update,
            osuUser.RenderSettings,
            Context.CancellationToken);
        if (preparation.Request is not { } preparedReplay)
        {
            await presentationService.EditPreparationFailureAsync(
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
            ReplayRenderWorkflowResult render = await workflowService.QueueAndWaitAsync(
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

            await presentationService.SendVideoAsync(
                Context.BotClient,
                render.StatusMessage,
                render.Job!,
                renderSettings,
                language,
                Context.CancellationToken);
        }
    }
}
