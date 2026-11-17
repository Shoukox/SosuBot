using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;

namespace SosuBot.Services.Ai;

// What the bot itself knows: its chats, its users and its commands.
public sealed partial class AiToolbox
{
    private const string CommandGuideFile = "ai-bot-commands.txt";

    private static readonly Lazy<string> CommandGuide = new(() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CommandGuideFile)).Replace("\r\n", "\n").Trim());

    private async Task<object> GetChatRankingAsync(JsonElement args, AiToolContext context,
        CancellationToken cancellationToken)
    {
        if (context.PrivateChat)
            return new { error = "this is a private chat, the chat ranking works only in groups" };

        var mode = (Playmode)ReadInt(args, "mode", 0, 3, 0);
        int count = ReadInt(args, "count", 1, 20, 10);
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();

        TelegramChat? chat = await database.TelegramChats.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChatId == context.ChatId, cancellationToken);
        List<long> memberIds = (chat?.ChatMembers ?? []).Distinct().ToList();
        if (memberIds.Count == 0)
            return new { error = "the bot knows no players of this chat yet" };

        HashSet<long> excluded = (chat!.ExcludeFromChatstats ?? []).ToHashSet();
        List<OsuUser> members = await database.OsuUsers.AsNoTracking()
            .Where(user => memberIds.Contains(user.TelegramId) && user.OsuUsername != "")
            .ToListAsync(cancellationToken);

        // The same rules as /chatstats: one row per osu! account, excluded players left out.
        var players = members
            .Where(user => !excluded.Contains(user.OsuUserId))
            .GroupBy(user => user.OsuUserId > 0 ? $"osu:{user.OsuUserId}" : user.OsuUsername.Trim().ToLowerInvariant())
            .Select(group => group.OrderByDescending(user => user.GetPP(mode)).First())
            .Where(user => user.GetPP(mode) > 0)
            .OrderByDescending(user => user.GetPP(mode))
            .Take(count)
            .Select((user, index) => new
            {
                Place = index + 1,
                Username = user.OsuUsername,
                Pp = Math.Round(user.GetPP(mode), 0),
                Accuracy = AccuracyOf(user, mode) is { } accuracy ? Math.Round(accuracy, 2) : (double?)null
            })
            .ToArray();

        return players.Length == 0
            ? new { error = $"no players of this chat have pp in {mode.ToGamemode()}" }
            : new
            {
                Mode = mode.ToGamemode(),
                Players = players,
                Note = "pp as the bot saved it last time, it can lag behind the profile a little"
            };
    }

    private async Task<object> GetBotUserAsync(JsonElement args, AiToolContext context,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();

        string username = ReadString(args, "telegram_username").TrimStart('@');
        long memberId = context.SpeakerId;
        if (username.Length > 0 && !telegramUsers.TryGetUserId(username, out memberId))
            return new { error = $"the bot has not seen @{username}" };

        // Someone else: only their linked nickname, and only for a member of this chat.
        if (memberId != context.SpeakerId)
        {
            TelegramChat? chat = await database.TelegramChats.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ChatId == context.ChatId, cancellationToken);
            if (context.PrivateChat || chat?.ChatMembers?.Contains(memberId) != true)
                return new { error = $"@{username} is not a member of this chat" };

            OsuUser? member = await database.OsuUsers.AsNoTracking()
                .FirstOrDefaultAsync(user => user.TelegramId == memberId, cancellationToken);
            return member is null || member.OsuUsername.Length == 0
                ? new { error = $"@{username} has not linked an osu! account (/set nickname)" }
                : new { TelegramUsername = username, OsuUsername = member.OsuUsername, Mode = member.OsuMode.ToGamemode() };
        }

        OsuUser? speaker = await database.OsuUsers.AsNoTracking()
            .FirstOrDefaultAsync(user => user.TelegramId == context.SpeakerId, cancellationToken);
        int[] trackedIds = context.PrivateChat
            ? []
            : await database.TrackedPlayerSubscriptions.AsNoTracking()
                .Where(subscription => subscription.ChatId == context.ChatId)
                .Select(subscription => subscription.PlayerId)
                .ToArrayAsync(cancellationToken);
        string[] tracked = await UsernamesAsync(trackedIds, cancellationToken);

        if (speaker is null || speaker.OsuUsername.Length == 0)
        {
            return new
            {
                Linked = false,
                Hint = "no osu! account linked, it is done with /set nickname",
                TrackedInThisChat = context.PrivateChat ? null : tracked
            };
        }

        return new
        {
            Linked = true,
            OsuUsername = speaker.OsuUsername,
            Link = speaker.OsuUserId > 0 ? $"https://osu.ppy.sh/users/{speaker.OsuUserId}" : null,
            DefaultMode = speaker.OsuMode.ToGamemode(),
            SavedStats = Enum.GetValues<Playmode>()
                .Where(mode => speaker.GetPP(mode) > 0)
                .Select(mode => new
                {
                    Mode = mode.ToGamemode(),
                    Pp = Math.Round(speaker.GetPP(mode), 0),
                    Accuracy = AccuracyOf(speaker, mode) is { } accuracy ? Math.Round(accuracy, 2) : (double?)null,
                    PlayTimeHours = PlayTimeOf(speaker, mode) / 3600
                })
                .ToArray(),
            StatsNote = "saved by the bot the last time this player used it; get_osu_user has live numbers",
            TrackedInThisChat = context.PrivateChat ? null : tracked
        };
    }

    private object GetBotHelp(JsonElement args)
    {
        string command = ReadString(args, "command").ToLowerInvariant();
        if (command.Length == 0)
            return new { Guide = CommandGuide.Value };

        command = "/" + command.TrimStart('/').Split('@', ' ')[0];
        var word = new Regex($@"(?<![\w/]){Regex.Escape(command)}(?!\w)", RegexOptions.IgnoreCase);
        string[] blocks = CommandGuide.Value.Split("\n\n")
            .Where(block => word.IsMatch(block.Split('\n')[0]))
            .ToArray();

        return blocks.Length > 0
            ? new { Command = command, Help = string.Join("\n\n", blocks) }
            : new { error = $"the bot has no command {command}", Guide = CommandGuide.Value };
    }

    /// <summary>osu! usernames for player ids in one request; ids it cannot resolve are left out.</summary>
    private async Task<string[]> UsernamesAsync(int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
            return [];

        string query = string.Join('&', ids.Take(50).Select(id => $"ids[]={id}"));
        JsonObject? response = await ApiAsync<JsonObject>($"/users?{query}", cancellationToken);
        return Items(response?["users"]).Select(user => Str(user, "username")).OfType<string>().ToArray();
    }

    private static double? AccuracyOf(OsuUser user, Playmode mode) => mode switch
    {
        Playmode.Taiko => user.TaikoAccuracyValue,
        Playmode.Catch => user.CatchAccuracyValue,
        Playmode.Mania => user.ManiaAccuracyValue,
        _ => user.StdAccuracyValue
    };

    private static long? PlayTimeOf(OsuUser user, Playmode mode) => mode switch
    {
        Playmode.Taiko => user.TaikoPlayTimeValue,
        Playmode.Catch => user.CatchPlayTimeValue,
        Playmode.Mania => user.ManiaPlayTimeValue,
        _ => user.StdPlayTimeValue
    };
}
