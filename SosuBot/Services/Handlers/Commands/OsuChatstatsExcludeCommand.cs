using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuChatstatsExcludeCommand : CommandBase<Message>
{
    public static string[] Commands = ["/exclude"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await waitMessage.ReplyAsync(Context.BotClient, language.error_nameIsEmpty);
            return;
        }

        var osuUsernameToExclude = parameters[0];
        var osuUserToExclude = Context.Database.OsuUsers.AsEnumerable().FirstOrDefault(m =>
            m.OsuUsername.Trim().Equals(osuUsernameToExclude.Trim(), StringComparison.InvariantCultureIgnoreCase));
        if (osuUserToExclude is null)
        {
            await waitMessage.ReplyAsync(Context.BotClient, language.error_userNotFoundInBotsDatabase);
            return;
        }

        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        if (chatInDatabase.ExcludeFromChatstats.Contains(osuUserToExclude.OsuUserId))
        {
            await waitMessage.ReplyAsync(Context.BotClient, language.error_excludeListAlreadyContainsThisId);
            return;
        }

        chatInDatabase.ExcludeFromChatstats.Add(osuUserToExclude!.OsuUserId);
        var sendText = language.command_excluded.Fill([osuUsernameToExclude]);
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}