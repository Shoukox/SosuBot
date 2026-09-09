using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
#pragma warning disable CS9113 // Parameter is unread.

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuCardCommand(
    BanchoApiV2 osuApi,
    OsuCardService cardService,
    RateLimiterFactory rateLimiterFactory) : CommandBase<Message>
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
        
        OsuCommandArguments.Player? arguments = OsuCommandArguments.ParsePlayer(Context.Update.Text!);
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
        await Context.BotClient.EditMessageMedia(
            waitMessage.Chat.Id,
            waitMessage.MessageId,
            new InputMediaPhoto(new InputFileStream(image, "osucard.png"))
            {
                Caption = caption,
                ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html
            },
            cancellationToken: Context.CancellationToken);
    }

}
