using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuChatstatsCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/chatstats", "/stats"];

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
        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var playmode = Playmode.Osu;
        if (parameters.Length == 1)
        {
            var ruleset = parameters[0].ParseToRuleset();
            if (ruleset is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                return;
            }

            playmode = ruleset.ParseRulesetToPlaymode();
        }

        var foundChatMembers = new List<OsuUser>();
        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        foreach (var memberId in chatInDatabase.ChatMembers!)
        {
            var foundMember = await _database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }

        foundChatMembers = foundChatMembers.DistinctBy(m => m.OsuUserId).OrderByDescending(m => m.GetPP(playmode)).Take(10)
            .ToList();

        var sendText = LocalizationMessageHelper.ChatstatsTitle(language, playmode.ToGamemode());

        var i = 1;
        foreach (var chatMember in foundChatMembers)
        {
            sendText += LocalizationMessageHelper.ChatstatsRow(language,
                $"{i}",
                $"{chatMember.OsuUsername}",
                $"{chatMember.GetPP(playmode):N2}"
            );
            i += 1;
        }

        sendText += language.command_chatstats_end;
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}


