using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class RandomBeatmapCommand(
    BeatmapsService beatmapsService,
    RateLimiterFactory rateLimiterFactory) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/rnd"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        string[] parameters = (Context.Update.Text!.GetCommandParameters() ?? [])
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
            .ToArray();

        if (parameters.Length > 1)
        {
            await Context.Update.ReplyAsync(Context.BotClient,
                $"{language.error_argsLength}\n{language.randomBeatmap_usage}");
            return;
        }

        Playmode playmode = Playmode.Osu;
        if (parameters.Length == 1)
        {
            string? ruleset = parameters[0].ParseToRuleset();
            if (ruleset is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_modeIncorrect);
                return;
            }

            playmode = ruleset.ParseRulesetToPlaymode();
        }

        TokenBucketRateLimiter rateLimiter = rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        long rateLimitKey = Context.Update.From?.Id ?? Context.Update.Chat.Id;
        if (!await rateLimiter.IsAllowedAsync(rateLimitKey.ToString(CultureInfo.InvariantCulture)))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        int? beatmapId = await beatmapsService.GetRandomCachedBeatmapIdAsync(playmode, Context.CancellationToken);
        if (beatmapId is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient,
                LocalizationMessageHelper.RandomBeatmapNoCachedBeatmaps(language, playmode.ToGamemode()));
            return;
        }

        BotContext database = Context.ServiceProvider.GetRequiredService<BotContext>();
        TelegramChat? chat = await database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        if (chat is not null) chat.LastBeatmapId = beatmapId;

        await Context.Update.ReplyAsync(Context.BotClient, $"{OsuConstants.BaseBeatmapUrl}{beatmapId}",
            linkPreviewEnabled: true);
    }
}
