using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class RenderCursorSizeCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/cursor"];
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        if (!double.TryParse(parameters[0], CultureInfo.InvariantCulture, out double cursorSize) || cursorSize < 0 || cursorSize > 2)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.render_settings_invalidCursorSize);
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
        await waitMessage.EditAsync(Context.BotClient, language.render_settings_cursorSizeUpdated.Fill([$"{cursorSize}"]));
        osuUserInDatabase.RenderSettings.CursorSize = cursorSize;
    }
}