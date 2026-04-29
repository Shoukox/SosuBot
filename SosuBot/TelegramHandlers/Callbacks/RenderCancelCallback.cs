using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Services;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Callbacks;

public class RenderCancelCallback() : CommandBase<CallbackQuery>
{
    public static readonly string Command = "render-cancel";
    private ReplayRenderService _replayRenderService = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();

        var parameters = Context.Update.Data!.Split(' ');
        var jobId = int.Parse(parameters[1]);
        var callbackUserId = Context.Update.From!.Id;

        var renderJob = await _replayRenderService.GetRenderJobInfo(jobId);
        if (renderJob == null)
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.callback_renderRequestNotFound, showAlert: true);
            return;
        }

        var expectedRequestedBy = $"telegram-user:{callbackUserId}";
        if (!string.Equals(renderJob.RequestedBy, expectedRequestedBy, StringComparison.Ordinal))
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.admin_accessDenied, showAlert: true);
            return;
        }

        try
        {
            var cancelled = await _replayRenderService.CancelRender(jobId);
            if (!cancelled)
            {
                await Context.Update.AnswerAsync(Context.BotClient, language.error_baseMessage, showAlert: true);
                return;
            }

            await Context.Update.AnswerAsync(Context.BotClient, language.render_cancel_success, showAlert: true);
        }
        catch
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.error_baseMessage, showAlert: true);
        }
    }
}
