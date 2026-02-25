using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Callbacks;

public class RenderStatusCallback() : CommandBase<CallbackQuery>
{
    public static readonly string Command = "render-status";
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

        var chatId = Context.Update.Message!.Chat.Id;

        var renderJob = await _replayRenderService.GetRenderJobInfo(jobId);
        if (renderJob == null)
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.callback_renderRequestNotFound, showAlert: true);
            return;
        }

        string renderProgressText = "???";
        if (renderJob.ProgressPercent < 0)
        {
            if (renderJob.ProgressPercent == -2)
            {
                renderProgressText = language.callback_rendererUploadingReplay;
            }
            else if (renderJob.ProgressPercent == -1)
            {
                renderProgressText = language.callback_rendererUploadingBeatmap;
            }
        }
        else if (renderJob.ProgressPercent == 0)
        {
            renderProgressText = language.callback_rendererInitializing;
        }
        else if (renderJob.ProgressPercent is > 0 and <= 0.95)
        {
            renderProgressText = LocalizationMessageHelper.CallbackRenderFinishedPercent(language, $"{renderJob.ProgressPercent:P0}");
        }
        else
        {
            renderProgressText = language.callback_rendererUploadingVideo;
        }
        await Context.Update.AnswerAsync(Context.BotClient, renderProgressText, showAlert: true);
    }
}


