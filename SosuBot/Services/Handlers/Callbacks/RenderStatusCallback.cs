using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using System.Diagnostics.Eventing.Reader;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Callbacks;

public class RenderStatusCallback() : CommandBase<CallbackQuery>
{
    public static readonly string Command = "render-status";
    private ApiV2 _osuApiV2 = null!;
    private ReplayRenderService _replayRenderService = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var chatId = long.Parse(parameters[0]);
        var jobId = int.Parse(parameters[2]);

        var renderJob = await _replayRenderService.GetRenderJobInfo(jobId);
        if (renderJob == null)
        {
            await Context.Update.AnswerAsync(Context.BotClient, "Запрос на рендер не найден в базе данных", showAlert: true);
            return;
        }

        string renderProgressText = "???";
        if (renderJob.ProgressPercent < 0)
        {
            if (renderJob.ProgressPercent == -2)
            {
                renderProgressText = $"Рендерер загружает реплей...";
            }
            else if (renderJob.ProgressPercent == -1)
            {
                renderProgressText = $"Рендерер загружает карту...";
            }
        }
        else if (renderJob.ProgressPercent == 0)
        {
            renderProgressText = "Инициализация. Ждем свободного рендерера...";
        }
        else if (renderJob.ProgressPercent is > 0 and <= 0.95)
        {
            renderProgressText = $"Рендер завершен на {renderJob.ProgressPercent:P0}";
        }
        else
        {
            renderProgressText = $"Рендерер загружает видео...";
        }
        await Context.Update.AnswerAsync(Context.BotClient, renderProgressText, showAlert: true);
    }
}