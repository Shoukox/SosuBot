using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuChatstatsIncludeCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/include"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                await waitMessage.ReplyAsync(Context.BotClient, language.error_nameIsEmpty);
                return;
            }

            string osuUsernameToExclude = parameters[0];
            OsuUser? osuUserToExclude = Context.Database.OsuUsers.AsEnumerable().FirstOrDefault(m => m.OsuUsername.Trim().ToLowerInvariant() == osuUsernameToExclude.Trim().ToLowerInvariant());
            if (osuUserToExclude is null)
            {
                await waitMessage.ReplyAsync(Context.BotClient, language.error_userNotFoundInBotsDatabase);
                return;
            }

            chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
            if (!chatInDatabase.ExcludeFromChatstats.Contains(osuUserToExclude.OsuUserId))
            {
                await waitMessage.ReplyAsync(Context.BotClient, language.error_userWasNotExcluded);
                return;
            }

            chatInDatabase.ExcludeFromChatstats.Remove(osuUserToExclude!.OsuUserId);
            string sendText = language.command_included.Fill([osuUsernameToExclude]);
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}
