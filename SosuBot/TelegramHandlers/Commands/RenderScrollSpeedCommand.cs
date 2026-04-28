using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class RenderScrollSpeedCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/scroll"];
    private ReplayRenderService _replayRenderService = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        if (!int.TryParse(parameters[0], out int scrollSpeed))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.render_settings_invalidScrollSpeed);
            return;
        }

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        // Fake 500ms wait
        await Task.Delay(500);
        await waitMessage.EditAsync(Context.BotClient, language.render_skin_uploadSuccess);
        osuUserInDatabase.RenderSettings.ManiaScrollSpeed = scrollSpeed;
    }
}



