using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuModeCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/mode"];

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var msgText = Context.Update.Text!;
        var parameters = msgText.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIsEmpty);
            return;
        }

        var osuMode = parameters[0].ParseToRuleset();

        if (osuMode is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIncorrect);
            return;
        }

        if (osuUserInDatabase is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        if (string.IsNullOrEmpty(osuMode))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIsEmpty);
            return;
        }

        osuUserInDatabase.OsuMode = osuMode.ParseRulesetToPlaymode();

        var sendText = language.command_setMode.Fill([osuUserInDatabase.OsuMode.ToGamemode()]);
        await Context.Update.ReplyAsync(Context.BotClient, sendText);
    }
}