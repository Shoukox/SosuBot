using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuChatstatsCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/chatstats", "/stats"];
    public static readonly string Description = "топ-10 игроков в чате";

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        if (Context.Update.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForGroups);
            return;
        }
        TelegramChat? chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        if (chatInDatabase is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_baseMessage);
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        Playmode playmode = Playmode.Osu;
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

        List<long> memberIds = (chatInDatabase.ChatMembers ?? []).Distinct().ToList();
        if (chatInDatabase.ChatMembers is null || !chatInDatabase.ChatMembers.SequenceEqual(memberIds))
            chatInDatabase.ChatMembers = memberIds;

        var foundChatMembers = new List<OsuUser>();
        chatInDatabase.ExcludeFromChatstats ??= [];
        foreach (long memberId in memberIds)
        {
            OsuUser? foundMember = await _database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }

        foundChatMembers = foundChatMembers
            .GroupBy(m => m.OsuUserId > 0 ? $"osu:{m.OsuUserId}" : $"telegram:{m.TelegramId}")
            .Select(group => group.OrderByDescending(m => m.GetPP(playmode)).First())
            .GroupBy(m => string.IsNullOrWhiteSpace(m.OsuUsername)
                ? $"telegram:{m.TelegramId}"
                : m.OsuUsername.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(m => m.GetPP(playmode)).First())
            .OrderByDescending(m => m.GetPP(playmode))
            .Take(10)
            .ToList();

        var sendText = LocalizationMessageHelper.ChatstatsTitle(language, playmode.ToGamemode());

        var i = 1;
        foreach (OsuUser chatMember in foundChatMembers)
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


