using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Callbacks;

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
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var jobId = int.Parse(parameters[1]);

        var chatId = Context.Update.Message!.Chat.Id;

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