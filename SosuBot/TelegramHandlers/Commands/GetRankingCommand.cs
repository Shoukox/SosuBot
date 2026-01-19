using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class GetRankingCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/ranking"];
    private ApiV2 _osuApiV2 = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
    }

    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var countryCode = parameters.Length > 0 ? parameters[0] : null;
        var users = await OsuApiHelper.GetUsersFromRanking(_osuApiV2, Playmode.Osu, countryCode, 20,
            Context.CancellationToken);

        if (users == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRankings);
            return;
        }

        var rankingText = "";
        for (var i = 0; i < users.Count; i++)
            rankingText +=
                $"{i + 1}. {UserHelper.GetUserProfileUrlWrappedInUsernameString(users[i].User!.Id.Value, users[i].User!.Username!)} - <b>{users[i].Pp:N2}pp💪</b>\n";

        var flagEmoji = countryCode == null ? "🌍" : UserHelper.CountryCodeToFlag(countryCode);
        var sendText = $"Топ игроков в {flagEmoji}:\n\n" +
                       rankingText;

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}