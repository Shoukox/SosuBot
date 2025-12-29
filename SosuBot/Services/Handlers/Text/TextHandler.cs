using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Text;

public sealed class TextHandler : CommandBase<Message>
{
    private ILogger<TextHandler> _logger = null!;
    private BotConfiguration _botConfig = null!;
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<TextHandler>>();
        _botConfig = Context.ServiceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        // If msg comes from a forwarded message of the bot itself, ignore it
        if (Context.Update.ForwardFrom?.Username == _botConfig.Username) return;

        ILocalization language = new Russian();
        await HandleBeatmapLink(language);
        await HandleUserProfileLink(language);
    }

    private async Task HandleUserProfileLink(ILocalization language)
    {
        var userProfileLink = OsuHelper.ParseOsuUserLink(Context.Update.GetAllLinks(), out var userId);
        if (userProfileLink == null) return;

        if (userProfileLink.EndsWith('-'))
        {
            _logger.LogInformation($"User profile link ends with '-', skipping gathering infos. Link: {userProfileLink}");
            return;
        }

        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            //await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        var user = (await _osuApiV2.Users.GetUser($"{userId}", new GetUserQueryParameters()))!.UserExtend!;

        var playmode = user.Playmode!.ParseRulesetToPlaymode();
        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(Context.Database, user, playmode, currentPp);

        DateTime.TryParse(user.JoinDate?.Value, out var registerDateTime);
        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{UserHelper.CountryCodeToFlag(user.CountryCode ?? "nn")}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(currentPp)}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
            $"{registerDateTime:dd.MM.yyyy HH:mm:ss}",
            $"{user.UserAchievements?.Length ?? 0}",
            $"{OsuConstants.TotalAchievementsCount}",
            $"{user.Statistics.GradeCounts!.SSH}",
            $"{user.Statistics.GradeCounts!.SH}",
            $"{user.Statistics.GradeCounts!.SS}",
            $"{user.Statistics.GradeCounts!.S}",
            $"{user.Statistics.GradeCounts!.A}"
        ]);
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
        {
            [
                new InlineKeyboardButton("Standard")
                    { CallbackData = $"{Context.Update.Chat.Id} user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko")
                    { CallbackData = $"{Context.Update.Chat.Id} user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch")
                    { CallbackData = $"{Context.Update.Chat.Id} user 2 {user.Username}" },
                new InlineKeyboardButton("Mania")
                    { CallbackData = $"{Context.Update.Chat.Id} user 3 {user.Username}" }
            ]
        });

        await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }

    private async Task HandleBeatmapLink(ILocalization language)
    {
        var beatmapLink = OsuHelper.ParseOsuBeatmapLink(Context.Update.GetAllLinks(), out var beatmapsetId,
            out var beatmapId);

        if (beatmapLink == null) return;
        if (beatmapLink.EndsWith('-'))
        {
            _logger.LogInformation($"Beatmap link ends with '-', skipping pp calculation. Link: {beatmapLink}");
            return;
        }

        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            //await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        BeatmapsetExtended? beatmapset = null;
        BeatmapExtended? beatmap = null;
        if (beatmapId is null && beatmapsetId is not null)
        {
            beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetId.Value, _osuApiV2);
            beatmapId = beatmapset!.Beatmaps!.OrderByDescending(m => m.DifficultyRating).First().Id;
        }

        beatmap ??= await _cachingHelper.GetOrCacheBeatmap(beatmapId!.Value, _osuApiV2);

        var hitobjectsSum = beatmap!.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
        bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;

        beatmapset ??= await _cachingHelper.GetOrCacheBeatmapset(beatmap.BeatmapsetId!.Value, _osuApiV2);

        var playmode = beatmap.Mode!.ParseRulesetToPlaymode();
        var classicMod = OsuHelper.GetClassicMode(playmode);

        osu.Game.Rulesets.Mods.Mod[] modsFromMessage = [];
        if (beatmapLink.Contains('+'))
        {
            string[] splittedMessage = beatmapLink.Split("+");
            if (splittedMessage[1].Length % 2 != 0) return;
            modsFromMessage = splittedMessage[1].ToMods(playmode).Except([classicMod]).ToArray();
        }

        var classicModsToApply = modsFromMessage.Concat([classicMod]).Distinct().ToArray();
        var lazerModsToApply = modsFromMessage.Distinct().ToArray();

        var ppCalculator = new PPCalculator();
        var calculatedPp = new
        {
            ClassicSS = (PPCalculationResult?)null,
            Classic99 = (PPCalculationResult?)null,
            Classic98 = (PPCalculationResult?)null,
            LazerSS = (PPCalculationResult?)null,
            Lazer99 = (PPCalculationResult?)null,
            Lazer98 = (PPCalculationResult?)null,
        };
        if (!beatmapContainsTooManyHitObjects)
        {
            var beatmapFile = await _beatmapsService.DownloadOrCacheBeatmap(beatmap.Id!.Value);
            calculatedPp = new
            {
                ClassicSS = await ppCalculator.CalculatePpAsync(
                                      beatmapId: beatmap.Id.Value,
                                      beatmapFile: beatmapFile,
                                      accuracy: 1,
                                      scoreMaxCombo: null,
                                      scoreMods: classicModsToApply,
                                      scoreStatistics: null,
                                      rulesetId: (int)playmode,
                                      cancellationToken: Context.CancellationToken),

                Classic99 = playmode == Playmode.Mania
                                      ? null
                                      : await ppCalculator.CalculatePpAsync(
                                          beatmapId: beatmap.Id.Value,
                                          beatmapFile: beatmapFile,
                                          accuracy: 0.99,
                                          scoreMaxCombo: null,
                                          scoreMods: classicModsToApply,
                                          scoreStatistics: null,
                                          rulesetId: (int)playmode,
                                          cancellationToken: Context.CancellationToken),

                Classic98 = playmode == Playmode.Mania
                                      ? null
                                      : await ppCalculator.CalculatePpAsync(
                                          beatmapId: beatmap.Id.Value,
                                          beatmapFile: beatmapFile,
                                          accuracy: 0.98,
                                          scoreMaxCombo: null,
                                          scoreMods: classicModsToApply,
                                          scoreStatistics: null,
                                          rulesetId: (int)playmode,
                                          cancellationToken: Context.CancellationToken),

                LazerSS = await ppCalculator.CalculatePpAsync(
                                          beatmapId: beatmap.Id.Value,
                                          beatmapFile: beatmapFile,
                                          accuracy: 1,
                                          scoreMaxCombo: null,
                                          scoreMods: lazerModsToApply,
                                          scoreStatistics: null,
                                          rulesetId: (int)playmode,
                                          cancellationToken: Context.CancellationToken),

                Lazer99 = playmode == Playmode.Mania
                                      ? null
                                      : await ppCalculator.CalculatePpAsync(
                                          beatmapId: beatmap.Id.Value,
                                          beatmapFile: beatmapFile,
                                          accuracy: 0.99,
                                          scoreMaxCombo: null,
                                          scoreMods: lazerModsToApply,
                                          scoreStatistics: null,
                                          rulesetId: (int)playmode,
                                          cancellationToken: Context.CancellationToken),

                Lazer98 = playmode == Playmode.Mania
                                      ? null
                                      : await ppCalculator.CalculatePpAsync(
                                          beatmapId: beatmap.Id.Value,
                                          beatmapFile: beatmapFile,
                                          accuracy: 0.98,
                                          scoreMaxCombo: null,
                                          scoreMods: lazerModsToApply,
                                          scoreStatistics: null,
                                          rulesetId: (int)playmode,
                                          cancellationToken: Context.CancellationToken)
            };
        }


        var duration = $"{beatmap.TotalLength / 60}m{beatmap.TotalLength % 60:00}s";
        var padLength = 9;

        string classicSSText = $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}", round: false)}%".PadRight(padLength) + "| " +
                               $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.Pp)}pp\n";
        string classic99Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.Pp)}pp\n";
        string classic98Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.Pp)}pp\n";
        string lazerSSText = $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}", round: false)}%".PadRight(padLength) + "| " +
                             $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.Pp)}pp\n";
        string lazer99Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.Pp)}pp\n";
        string lazer98Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.Pp)}pp\n";

        // Calculate diff rating
        double? difficultyRatingForGivenMods = calculatedPp.Lazer98?.DifficultyAttributes.StarRating;
        if (difficultyRatingForGivenMods == null)
        {
            var beatmapAttributesResponse = await _osuApiV2.Beatmaps.GetBeatmapAttributes(beatmap.Id.Value, new() { RulesetId = ((int)playmode).ToString(), Mods = lazerModsToApply.Select(m => new Mod { Acronym = m.Acronym }).ToArray() });
            difficultyRatingForGivenMods = beatmapAttributesResponse?.DifficultyAttributes?.StarRating;
        }

        var ar = beatmap.AR.ToString();
        if (playmode is Playmode.Mania or Playmode.Taiko)
        {
            ar = "—";
        }

        var textToSend = language.send_mapInfo.Fill([
            $"{playmode.ToGamemode()}",
            $"{beatmap.Version.EncodeHtml()}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(beatmap.DifficultyRating, round: false)}",
            $"{duration}",
            $"{beatmapset!.Creator}",
            $"{beatmap.Status}",
            $"{beatmap.Id}",
            $"{beatmap.CS}",
            $"{ar}",
            $"{beatmap.Drain}",
            $"{beatmap.BPM}",
            $"{lazerModsToApply.ModsToString(playmode)}",

            $"{_scoreHelper.GetFormattedNumConsideringNull(difficultyRatingForGivenMods, round: false)}",

            classicSSText,
            classic99Text,
            classic98Text,
            lazerSSText,
            lazer99Text,
            lazer98Text,
        ]);

        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song preview")
        { CallbackData = $"{Context.Update.Chat.Id} songpreview {beatmapset.Id}" });

        if (beatmapContainsTooManyHitObjects)
            textToSend += "\nВ карте слишком много объектов, пп расчет не будет проведен.";

        // Get beatmapset cover from cache
        InputFile cover = await _cachingHelper.GetOrCacheBeatmapsetCover(beatmapset.Id!.Value);

        try
        {
            await Context.Update.ReplyPhotoAsync(Context.BotClient, cover, textToSend,
                replyMarkup: ik);
        }
        catch
        {
            await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }

        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        chatInDatabase!.LastBeatmapId = beatmapId;
    }
}