using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
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
        ILocalization language = Context.GetLocalization();
        var parameters = Context.Update.Text!.GetCommandParameters()!.ToArray();
        if (parameters.Length == 0)
        {
            await Context.Update.ReplyAsync(Context.BotClient,
                $"{Commands[0]} <i>number</i>\n" +
                $"{Commands[0]} 1\n" +
                $"{Commands[0]} 1.5");
            return;
        }
        if (!double.TryParse(parameters[0], CultureInfo.InvariantCulture, out double cursorSize) || !double.IsFinite(cursorSize) || cursorSize < 0.1 || cursorSize > 2)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.render_settings_invalidCursorSize);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
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