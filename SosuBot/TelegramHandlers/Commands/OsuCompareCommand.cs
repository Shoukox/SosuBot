using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuCompareCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/compare", "/cmp"];
    public static readonly string Description = "[user1] [user2] [mode] сравнить игроков";
    private BanchoApiV2 _osuApiV2 = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        CompareArguments? compareArguments;
        bool invalidMode = false;
        //compareArguments = ParseLegacyArguments(Context.Update.Text!, osuUserInDatabase, out invalidMode);
        OsuCommandArguments.Parsed? parsedArguments = OsuCommandArguments.Parse(Context.Update.Text!);
        compareArguments = parsedArguments is null
            ? null
            : ParseExtendedArguments(parsedArguments, osuUserInDatabase);

        if (compareArguments is null)
        {
            if (invalidMode)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIncorrect);
                return;
            }

            await waitMessage.EditAsync(Context.BotClient, language.command_compare_usage);
            return;
        }

        string user1IdAsString = compareArguments.User1;
        string user2IdAsString = compareArguments.User2;
        string ruleset = compareArguments.Ruleset;

        GetUserResponse? getUser1Response =
            await _osuApiV2.Users.GetUser($"@{user1IdAsString}", new GetUserQueryParameters(), ruleset);
        GetUserResponse? getUser2Response =
            await _osuApiV2.Users.GetUser($"@{user2IdAsString}", new GetUserQueryParameters(), ruleset);

        if (getUser1Response == null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                LocalizationMessageHelper.ErrorSpecificUserNotFound(language, user1IdAsString));
            return;
        }

        if (getUser2Response == null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                LocalizationMessageHelper.ErrorSpecificUserNotFound(language, user2IdAsString));
            return;
        }

        UserExtend user1 = getUser1Response.UserExtend!;
        UserExtend user2 = getUser2Response.UserExtend!;

        var acc1 = $"{user1.Statistics!.HitAccuracy:N2}%";
        var acc2 = $"{user2.Statistics!.HitAccuracy:N2}%";

        var max = new[]
        {
            (user1.Statistics.CountryRank + "# UZ").Length,
            (user1.Statistics.GlobalRank + "#").Length,
            (user1.Statistics.Pp!.Value.ToString("N2") + "pp").Length,
            acc1.Length, $"{user1.Statistics.PlayTime}h".Length,
            user1.Username!.Length
        }.Max();

        string username1 = user1.Username!;
        if (username1.Length >= 12)
        {
            username1 = username1[..9] + "...";
        }

        string username2 = user2.Username!;
        if (username2.Length >= 12)
        {
            username2 = username2[..9] + "...";
        }

        var textToSend = LocalizationMessageHelper.CommandCompare(language,
            ruleset.ParseRulesetToGamemode(),
            username1.PadRight(max),
            username2,
            ("#" + user1.Statistics.GlobalRank.ReplaceIfNull()).PadRight(max - 1),
            $"#{user2.Statistics.GlobalRank.ReplaceIfNull()}",
            ("#" + user1.Statistics.CountryRank.ReplaceIfNull() + " " + user1.CountryCode).PadRight(max - 1),
            "#" + user2.Statistics.CountryRank.ReplaceIfNull() + " " + user2.CountryCode,
            (user1.Statistics.Pp!.Value.ToString("N2") + "pp").PadRight(max),
            user2.Statistics.Pp!.Value.ToString("N2") + "pp",
            acc1.PadRight(max),
            acc2,
            $"{(user1.Statistics.PlayTime!.Value / 3600 + "h").PadRight(max)}",
            $"{user2.Statistics.PlayTime!.Value / 3600}h"
        );
        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }

    private static CompareArguments? ParseExtendedArguments(
        OsuCommandArguments.Parsed parsedArguments,
        OsuUser? osuUserInDatabase)
    {
        if (parsedArguments.Parameters.Count == 0)
            return null;

        bool hasSeparator = parsedArguments.Parameters.Any(parameter => parameter == "|");
        string? firstUsername;
        string? secondUsername;
        if (!hasSeparator && parsedArguments.Parameters.Count == 1)
        {
            if (osuUserInDatabase is null)
                return null;

            firstUsername = osuUserInDatabase.OsuUsername;
            secondUsername = parsedArguments.Parameters[0];
        }
        else if (!hasSeparator && parsedArguments.Parameters.Count > 2 && osuUserInDatabase is not null)
        {
            firstUsername = osuUserInDatabase.OsuUsername;
            secondUsername = OsuCommandArguments.JoinUsername(parsedArguments.Parameters);
        }
        else if (!OsuCommandArguments.TrySplitPlayers(parsedArguments,
                     out firstUsername, out secondUsername))
        {
            return null;
        }

        return new CompareArguments(firstUsername!, secondUsername!,
            parsedArguments.Playmode?.ToRuleset() ?? Ruleset.Osu);
    }

    private static CompareArguments? ParseLegacyArguments(
        string messageText,
        OsuUser? osuUserInDatabase,
        out bool invalidMode)
    {
        invalidMode = false;
        string[] parameters = messageText.GetCommandParameters() ?? [];
        if (parameters.Length == 0 ||
            (parameters.Length == 1 && osuUserInDatabase is null) ||
            parameters.Length > 3)
        {
            return null;
        }

        string firstUsername;
        string secondUsername;
        if (parameters.Length == 1)
        {
            firstUsername = osuUserInDatabase!.OsuUsername;
            secondUsername = parameters[0];
        }
        else
        {
            firstUsername = parameters[0];
            secondUsername = parameters[1];
        }

        string ruleset = Ruleset.Osu;
        if (parameters.Length == 3)
        {
            string? parsedRuleset = parameters[2].ParseToRuleset();
            if (parsedRuleset is null)
            {
                invalidMode = true;
                return null;
            }

            ruleset = parsedRuleset;
        }

        return new CompareArguments(firstUsername, secondUsername, ruleset);
    }

    private sealed record CompareArguments(string User1, string User2, string Ruleset);
}