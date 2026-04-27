using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using Serilog.Parsing;
using SosuBot.Configuration;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.PerformanceCalculator;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Text;

public sealed class TextHandler : CommandBase<Message>
{
    private BotConfiguration _botConfig = null!;
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;
    private BotContext _database = null!;
    private ILogger<TextHandler> _logger = null!;

    private TokenBucketRateLimiter _tokenBucketRateLimiter = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _botConfig = Context.ServiceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<TextHandler>>();

        _tokenBucketRateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        if (Context.Update.IsForwardedFrom(_botConfig.Id)) return;

        var language = Context.GetLocalization();
        await HandleBeatmapLink(language);
        await HandleUserProfileLink(language);
        await HandleScoreLink(language);
    }

    private async Task HandleScoreLink(ILocalization language)
    {
        if (OsuHelper.ParseOsuScoreLink(Context.Update.GetAllLinks(), out long? scoreId) == null) return;
        if (!await _tokenBucketRateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}")) return;

        Score? score = await _osuApiV2.Scores.GetScore(scoreId!.Value);
        if (score == null) return;

        BeatmapExtended? beatmap = await _cachingHelper.GetOrCacheBeatmap(score.BeatmapId!.Value, _osuApiV2);
        if (beatmap is null) return;

        BeatmapsetExtended? beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmap.BeatmapsetId.Value, _osuApiV2);
        if (beatmapset is null) return;

        Playmode currentPlaymode = (Playmode)beatmap.ModeInt!.Value;

        string textToSend = LocalizationMessageHelper.CommandScore(language,
            $"{_scoreHelper.GetScoreRankEmoji(score.Rank)}{_scoreHelper.ParseScoreRank(score.Rank!)}",
            $"{beatmap.Url}",
            $"{beatmapset!.Title.EncodeHtml()}",
            $"{beatmap.Version.EncodeHtml()}",
            $"{beatmap.Status}",
            $"{_scoreHelper.GetScoreStatisticsText(score.Statistics!, currentPlaymode)}",
            $"{score.Statistics!.Miss}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
            $"{_scoreHelper.GetModsText(score.Mods!)}",
            $"{score.MaxCombo}",
            $"{beatmap.MaxCombo}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(score.Pp)}",
            $"({score.EndedAt!.Value:dd.MM.yyyy HH:mm}) {_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}"
        );

        if (score.HasReplay == true)
        {
            textToSend += language.score_replayAvailable;
        }

        await Context.Update.ReplyAsync(Context.BotClient, textToSend);
    }

    private async Task HandleUserProfileLink(ILocalization language)
    {
        if (OsuHelper.ParseOsuUserLink(Context.Update.GetAllLinks(), out var userId) == null) return;
        if (!await _tokenBucketRateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}")) return;

        var user = (await _osuApiV2.Users.GetUser($"{userId}", new GetUserQueryParameters()))?.UserExtend;
        if (user == null) return;

        var playmode = user.Playmode!.ParseRulesetToPlaymode();
        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText = await UserHelper.GetPpDifferenceTextAsync(_database, user, playmode, currentPp);

        var textToSend = LocalizationMessageHelper.UserProfileText(
            language,
            _scoreHelper,
            user,
            playmode,
            currentPp,
            ppDifferenceText,
            $"{OsuConstants.TotalAchievementsCount}");

        await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: UserHelper.BuildUserModeKeyboard(user.Username!));
    }

    private async Task HandleBeatmapLink(ILocalization language)
    {
        var beatmapLink = OsuHelper.ParseOsuBeatmapLink(Context.Update.GetAllLinks(), out var beatmapsetId, out var beatmapId);
        if (beatmapLink == null) return;

        if (beatmapLink.EndsWith('-'))
        {
            _logger.LogInformation(language.text_beatmapLinkSkipLog, beatmapLink);
            return;
        }

        if (!await _tokenBucketRateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}")) return;

        BeatmapsetExtended? beatmapset = null;
        if (beatmapId is null && beatmapsetId is not null)
        {
            beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetId.Value, _osuApiV2);
            beatmapId = beatmapset!.Beatmaps!.OrderByDescending(m => m.DifficultyRating).First().Id;
        }

        if (beatmapId is null) return;

        var beatmap = await _cachingHelper.GetOrCacheBeatmap(beatmapId.Value, _osuApiV2);
        if (beatmap == null) return;

        var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
        bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;

        beatmapset ??= await _cachingHelper.GetOrCacheBeatmapset(beatmap.BeatmapsetId!.Value, _osuApiV2);
        if (beatmapset == null) return;

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

        var calculatedPp = await CalculateBeatmapPpAsync(beatmap, playmode, classicModsToApply, lazerModsToApply, beatmapContainsTooManyHitObjects);

        int totalLengthConsideringMods = (int)(beatmap.TotalLength!.Value / calculatedPp.LazerSS!.SpeedChangeFactor);
        var duration = $"{_scoreHelper.GetFormattedNumConsideringNull(totalLengthConsideringMods / 60, round: false, format: "#")}m{_scoreHelper.GetFormattedNumConsideringNull(totalLengthConsideringMods % 60, round: false, format: "00")}s";
        var padLength = 9;

        string classicSSText = $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}", round: false)}%".PadRight(padLength) + "| " +
                               $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.ClassicSS?.PP)}pp\n";
        string classic99Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic99?.PP)}pp\n";
        string classic98Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Classic98?.PP)}pp\n";
        string lazerSSText = $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.CalculatedAccuracy * 100, defaultValue: $"{100:N2}", round: false)}%".PadRight(padLength) + "| " +
                             $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.PP)}pp\n";
        string lazer99Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.CalculatedAccuracy * 100, defaultValue: $"{99:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer99?.PP)}pp\n";
        string lazer98Text = playmode == Playmode.Mania
            ? ""
            : $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.CalculatedAccuracy * 100, defaultValue: $"{98:N2}", round: false)}%".PadRight(padLength) + "| " +
              $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.Lazer98?.PP)}pp\n";

        double? difficultyRatingForGivenMods = calculatedPp.Lazer98?.DifficultyAttributes.StarRating;
        if (difficultyRatingForGivenMods == null)
        {
            var beatmapAttributesResponse = await _osuApiV2.Beatmaps.GetBeatmapAttributes(beatmap.Id.Value,
                new() { RulesetId = ((int)playmode).ToString(), Mods = lazerModsToApply.Select(m => new Mod { Acronym = m.Acronym }).ToArray() });
            difficultyRatingForGivenMods = beatmapAttributesResponse?.DifficultyAttributes?.StarRating;
        }

        string ar = beatmap.ModeInt is (int)Playmode.Mania or (int)Playmode.Taiko ? "—" : calculatedPp.LazerSS!.AR.ToString();

        var textToSend = LocalizationMessageHelper.SendMapInfo(language,
            $"{playmode.ToGamemode()}",
            $"{lazerModsToApply.ModsToString(playmode)}",
            $"{beatmap.Version.EncodeHtml()}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(difficultyRatingForGivenMods, round: false)}",
            $"{duration}",
            $"{beatmapset.Creator}",
            $"{beatmap.Status}",
            $"{beatmap.Id}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.CS, round: false, format: "N1")}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.AR, round: false, format: "N1")}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.HP, round: false, format: "N1")}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(calculatedPp.LazerSS?.SpeedChangeFactor * beatmap.BPM, round: false)}",
            classicSSText,
            classic99Text,
            classic98Text,
            lazerSSText,
            lazer99Text,
            lazer98Text
        );

        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton(language.text_songPreviewButton) { CallbackData = $"songpreview {beatmapset.Id}" });

        if (beatmapContainsTooManyHitObjects)
            textToSend += $"\n{language.text_tooManyObjectsNoPp}";

        InputFile cover = await _cachingHelper.GetOrCacheBeatmapsetCover(beatmapset.Id!.Value);

        try
        {
            await Context.Update.ReplyPhotoAsync(Context.BotClient, cover, textToSend, replyMarkup: ik);
        }
        catch
        {
            await Context.Update.ReplyAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }

        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        chatInDatabase!.LastBeatmapId = beatmapId;
    }

    private async Task<(PPCalculationResult? ClassicSS, PPCalculationResult? Classic99, PPCalculationResult? Classic98, PPCalculationResult? LazerSS, PPCalculationResult? Lazer99, PPCalculationResult? Lazer98)> CalculateBeatmapPpAsync(
        BeatmapExtended beatmap,
        Playmode playmode,
        osu.Game.Rulesets.Mods.Mod[] classicModsToApply,
        osu.Game.Rulesets.Mods.Mod[] lazerModsToApply,
        bool beatmapContainsTooManyHitObjects)
    {
        if (beatmapContainsTooManyHitObjects)
            return (null, null, null, null, null, null);

        var ppCalculator = new PPCalculator();
        var beatmapFile = await _beatmapsService.DownloadOrCacheBeatmap(beatmap.Id!.Value);

        var classicSS = await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 1,
            scoreMaxCombo: null,
            scoreMods: classicModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        var classic99 = playmode == Playmode.Mania ? null : await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 0.99,
            scoreMaxCombo: null,
            scoreMods: classicModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        var classic98 = playmode == Playmode.Mania ? null : await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 0.98,
            scoreMaxCombo: null,
            scoreMods: classicModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        var lazerSS = await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 1,
            scoreMaxCombo: null,
            scoreMods: lazerModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        var lazer99 = playmode == Playmode.Mania ? null : await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 0.99,
            scoreMaxCombo: null,
            scoreMods: lazerModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        var lazer98 = playmode == Playmode.Mania ? null : await ppCalculator.CalculatePpAsync(
            beatmapId: beatmap.Id.Value,
            beatmapFile: beatmapFile,
            accuracy: 0.98,
            scoreMaxCombo: null,
            scoreMods: lazerModsToApply,
            scoreStatistics: null,
            rulesetId: (int)playmode,
            cancellationToken: Context.CancellationToken);

        return (classicSS, classic99, classic98, lazerSS, lazer99, lazer98);
    }
}



