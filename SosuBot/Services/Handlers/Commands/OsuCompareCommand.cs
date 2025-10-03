using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuCompareCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/compare", "/cmp"];
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();
        
        ILocalization language = new Russian();

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        if (parameters.Length < 2)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        var ruleset = Ruleset.Osu;
        if (parameters.Length >= 3)
        {
            ruleset = parameters[2].ParseToRuleset();
            if (ruleset is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIncorrect);
                return;
            }
        }

        var getUser1Response =
            await _osuApiV2.Users.GetUser($"@{parameters[0]}", new GetUserQueryParameters(), ruleset);
        var getUser2Response =
            await _osuApiV2.Users.GetUser($"@{parameters[1]}", new GetUserQueryParameters(), ruleset);

        if (getUser1Response == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_specificUserNotFound.Fill([parameters[0]]));
            return;
        }

        if (getUser2Response == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_specificUserNotFound.Fill([parameters[1]]));
            return;
        }

        var user1 = getUser1Response.UserExtend!;
        var user2 = getUser2Response.UserExtend!;

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

        var textToSend = language.command_compare.Fill([
            ruleset.ParseRulesetToGamemode(),

            user1.Username.PadRight(max),
            user2.Username!,

            ("#" + user1.Statistics.GlobalRank.ReplaceIfNull()).PadRight(max),
            $"#{user2.Statistics.GlobalRank.ReplaceIfNull()}",

            ("#" + user1.Statistics.CountryRank.ReplaceIfNull() + " " + user1.CountryCode).PadRight(max),
            "#" + user2.Statistics.CountryRank.ReplaceIfNull() + " " + user2.CountryCode,

            (user1.Statistics.Pp!.Value.ToString("N2") + "pp").PadRight(max),
            user2.Statistics.Pp!.Value.ToString("N2") + "pp",

            acc1.PadRight(max),
            acc2,

            $"{(user1.Statistics.PlayTime!.Value / 3600 + "h").PadRight(max)}",
            $"{user2.Statistics.PlayTime!.Value / 3600}h"
        ]);
        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}