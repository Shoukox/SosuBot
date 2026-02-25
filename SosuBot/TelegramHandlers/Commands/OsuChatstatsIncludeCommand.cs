using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuChatstatsIncludeCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/include"];

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        if (Context.Update.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForGroups);
            return;
        }
        var chatAdmins = await Context.BotClient.GetChatAdministrators(Context.Update.Chat.Id);
        if (!chatAdmins.Any(m => m.User.Id == Context.Update.From?.Id))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForAdmins);
            return;
        }
        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_nameIsEmpty);
            return;
        }

        var osuUsernameToExclude = parameters[0];
        var osuUserToExclude = _database.OsuUsers.AsEnumerable().FirstOrDefault(m =>
            m.OsuUsername.Trim().ToLowerInvariant() == osuUsernameToExclude.Trim().ToLowerInvariant());
        if (osuUserToExclude is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotFoundInBotsDatabase);
            return;
        }

        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        if (!chatInDatabase.ExcludeFromChatstats.Contains(osuUserToExclude.OsuUserId))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userWasNotExcluded);
            return;
        }

        chatInDatabase.ExcludeFromChatstats.Remove(osuUserToExclude.OsuUserId);
        var sendText = LocalizationMessageHelper.ChatstatsIncluded(language, osuUsernameToExclude);
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}


