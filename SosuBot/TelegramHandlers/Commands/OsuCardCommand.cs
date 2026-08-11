using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuCardCommand(
    BanchoApiV2 osuApi,
    OsuCardService cardService,
    RateLimiterFactory rateLimiterFactory,
    ILogger<OsuCardCommand> logger) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/osucard"];
    public static readonly string Description = "[user] [mode] карточка навыков игрока osu!";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        long rateLimitKey = Context.Update.From?.Id ?? Context.Update.Chat.Id;
        if (!await rateLimiter.IsAllowedAsync(rateLimitKey.ToString(CultureInfo.InvariantCulture)))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        ParsedArguments? arguments = ParseArguments(Context.Update.Text!);
        if (arguments is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.osuCard_usage);
            return;
        }

        BotContext database = Context.ServiceProvider.GetRequiredService<BotContext>();
        OsuUser? registeredUser = await database.OsuUsers.FindAsync(
            [Context.Update.From!.Id], Context.CancellationToken);
        string username;
        Playmode? playmode;

        if (string.IsNullOrWhiteSpace(arguments.Username))
        {
            if (registeredUser is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.osuCard_usage);
                return;
            }

            username = registeredUser.OsuUsername;
            playmode = arguments.Playmode ?? registeredUser.OsuMode;
        }
        else
        {
            if (arguments.Username.StartsWith('@'))
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_dontUseTelegramUsername);
                return;
            }

            username = arguments.Username;
            playmode = arguments.Playmode;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        GetUserResponse? userResponse = playmode is { } selectedPlaymode
            ? await osuApi.Users.GetUser(
                $"@{username}",
                new GetUserQueryParameters(),
                selectedPlaymode.ToRuleset(),
                Context.CancellationToken)
            : await osuApi.Users.GetUser($"@{username}", new GetUserQueryParameters(),
                cancellationToken: Context.CancellationToken);
        UserExtend? user = userResponse?.UserExtend;
        if (user is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
            return;
        }

        playmode ??= user.Playmode is { } userRuleset
            ? userRuleset.ParseRulesetToPlaymode()
            : Playmode.Osu;
        OsuCardGenerationResult result = await cardService.GenerateAsync(user, playmode.Value,
            Context.CancellationToken);
        if (result.Failure == OsuCardGenerationFailure.NoScores)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noBestScores);
            return;
        }

        if (result.Failure != OsuCardGenerationFailure.None || result.Image is null || result.Skills is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.osuCard_calculationFailed);
            return;
        }

        string caption = language.osuCard_caption.Fill([
            user.Username!.EncodeHtml()!,
            playmode.Value.ToGamemode(),
            result.Skills.CalculatedScores.ToString(CultureInfo.InvariantCulture),
            result.RequestedScores.ToString(CultureInfo.InvariantCulture)
        ]);

        using MemoryStream image = new(result.Image, writable: false);
        await Context.Update.ReplyPhotoAsync(
            Context.BotClient,
            new InputFileStream(image, "osucard.png"),
            caption);

        try
        {
            await Context.BotClient.DeleteMessage(waitMessage.Chat.Id, waitMessage.MessageId,
                Context.CancellationToken);
        }
        catch (ApiRequestException exception)
        {
            logger.LogDebug(exception, "Could not delete the /osucard progress message");
        }
    }

    private static ParsedArguments? ParseArguments(string messageText)
    {
        List<string> parameters = (messageText.GetCommandParameters() ?? [])
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
            .ToList();

        Playmode? playmode = null;
        string? keywordMode = parameters.FirstOrDefault(parameter =>
            parameter.StartsWith("mode=", StringComparison.OrdinalIgnoreCase));
        if (keywordMode is not null)
        {
            string? ruleset = keywordMode.ParseToRuleset();
            if (ruleset is null || parameters.Count(parameter =>
                    parameter.StartsWith("mode=", StringComparison.OrdinalIgnoreCase)) != 1)
                return null;

            playmode = ruleset.ParseRulesetToPlaymode();
            parameters.Remove(keywordMode);
        }

        if (parameters.Count >= 2 && parameters[^1].ParseToRuleset() is { } trailingRuleset)
        {
            if (playmode is not null)
                return null;

            playmode = trailingRuleset.ParseRulesetToPlaymode();
            parameters.RemoveAt(parameters.Count - 1);
        }
        else if (parameters.Count == 1 && playmode is null && parameters[0].ParseToRuleset() is { } onlyRuleset)
        {
            playmode = onlyRuleset.ParseRulesetToPlaymode();
            parameters.Clear();
        }

        if (parameters.Any(parameter => parameter.Contains('=')))
            return null;

        string? username = parameters.Count == 0 ? null : string.Join(' ', parameters);
        return new ParsedArguments(username, playmode);
    }

    private sealed record ParsedArguments(string? Username, Playmode? Playmode);
}
