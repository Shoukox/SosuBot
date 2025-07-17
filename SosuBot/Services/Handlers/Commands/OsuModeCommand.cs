using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuModeCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/mode"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            string msgText = Context.Update.Text!;
            string[] parameters = msgText.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIsEmpty);
                return;
            }

            string? osuMode = parameters[0].ParseToRuleset();

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

            string sendText = language.command_setMode.Fill([osuUserInDatabase.OsuMode.ToGamemode()]);
            await Context.Update.ReplyAsync(Context.BotClient, sendText);
        }
    }
}
