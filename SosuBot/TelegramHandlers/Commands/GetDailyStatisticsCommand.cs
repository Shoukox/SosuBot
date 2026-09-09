using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Globalization;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public class GetDailyStatisticsCommand : CommandBase<Message>
{
    protected const string DateFormat = "dd.MM.yyyy";

    public static readonly string[] Commands = ["/daily_stats", "/get"];
    public static readonly string Description = "показать дневную статистику";

    private BanchoApiV2 _osuApiV2 = null!;
    private ILogger<GetDailyStatisticsCommand> _logger = null!;
    private ScoreHelper _scoreHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<GetDailyStatisticsCommand>>();
    }

    public override async Task ExecuteAsync()
    {
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        ILocalization language = Context.GetLocalization();
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
        await Task.Delay(500);

        string[] parameters = Context.Update.Text!.GetCommandParameters() ?? [];
        string sendText;
        if (parameters.Length == 0 && !UsesImplicitPeriod)
        {
            await waitMessage.EditAsync(Context.BotClient, GetUsageText(language));
            return;
        }

        string? ruleset = TextHelper.GetPlaymodeFromParameters(parameters, out parameters)?.ToRuleset();
        if (ruleset is null && parameters.Length > 0)
        {
            ruleset = parameters[0].ParseToRuleset();
            if (ruleset is not null)
                parameters = parameters[1..];
        }
        ruleset ??= Ruleset.Osu;

        DateTime? rangeStart;
        DateTime? rangeEnd;
        if (UsesImplicitPeriod)
        {
            if (parameters.Length != 0)
            {
                await waitMessage.EditAsync(Context.BotClient, GetUsageText(language));
                return;
            }

            rangeEnd = GetTashkentToday();
            rangeStart = GetImplicitPeriodStart(rangeEnd.Value);
        }
        else if (parameters.Length == 0)
        {
            rangeStart = null;
            rangeEnd = null;
        }
        else if (parameters.Length == 1 && TryParseDateRange(parameters[0], out rangeStart, out rangeEnd))
        {
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, GetUsageText(language));
            return;
        }

        Playmode playmode = ruleset.ParseRulesetToPlaymode();
        List<DailyStatistics> dailyStats = rangeStart is { } start && rangeEnd is { } end
            ? _database.DailyStatistics
                .Where(statistics => statistics.DayOfStatistic >= start &&
                                     statistics.DayOfStatistic < end.AddDays(1))
                .OrderBy(statistics => statistics.DayOfStatistic)
                .ThenBy(statistics => statistics.Id)
                .ToList()
            : [_database.DailyStatistics.OrderByDescending(statistics => statistics.Id).FirstOrDefault()!];

        DailyStatistics? aggregatedStats = Aggregate(
            dailyStats.Where(statistics => statistics is not null).ToList(), rangeEnd);
        if (aggregatedStats is null ||
            aggregatedStats.Scores.Count == 0 ||
            aggregatedStats.ActiveUsers.Count == 0 ||
            aggregatedStats.Scores.All(score => score.ScoreJson.ModeInt != (int)playmode))
        {
            sendText = (rangeStart is not null && !UsesImplicitPeriod) ? GetUsageText(language) : language.error_noRecords;
            await waitMessage.EditAsync(Context.BotClient, sendText);
            return;
        }

        sendText = await _scoreHelper.GetDailyStatisticsSendText(playmode, aggregatedStats, _osuApiV2, language);
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }

    protected virtual bool UsesImplicitPeriod => false;

    protected virtual DateTime GetImplicitPeriodStart(DateTime today) => today;

    protected static DateTime GetTashkentToday()
    {
        TimeZoneInfo tashkent = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tashkent).Date;
    }

    protected static DateTime StartOfWeek(DateTime date)
    {
        int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday).Date;
    }

    private static bool TryParseDateRange(string text, out DateTime? start, out DateTime? end)
    {
        start = null;
        end = null;
        string[] dates = text.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dates.Length is < 1 or > 2 || !DateTime.TryParseExact(
                dates[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
            return false;

        DateTime parsedEnd = parsedStart;
        if (dates.Length == 2 && !DateTime.TryParseExact(
                dates[1], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedEnd))
            return false;
        if (parsedEnd < parsedStart)
            return false;

        start = parsedStart.Date;
        end = parsedEnd.Date;
        return true;
    }

    private static DailyStatistics? Aggregate(IReadOnlyList<DailyStatistics> statistics, DateTime? rangeEnd)
    {
        if (statistics.Count == 0)
            return null;

        DailyStatistics first = statistics[0];
        return new DailyStatistics
        {
            Id = statistics[^1].Id,
            DayOfStatistic = rangeEnd ?? first.DayOfStatistic,
            CountryCode = first.CountryCode,
            ActiveUsers = statistics.SelectMany(item => item.ActiveUsers).DistinctBy(item => item.UserId).ToList(),
            BeatmapsPlayed = statistics.SelectMany(item => item.BeatmapsPlayed).Distinct().ToList(),
            Scores = statistics.SelectMany(item => item.Scores).DistinctBy(item => item.ScoreId).ToList()
        };
    }

    protected string GetUsageText(ILocalization language)
    {
        DailyStatistics? firstAvailableDate = _database.DailyStatistics
            .Where(statistics => statistics.Scores.Any())
            .OrderBy(statistics => statistics.DayOfStatistic)
            .ThenBy(statistics => statistics.Id)
            .FirstOrDefault();
        DailyStatistics? lastAvailableDate = _database.DailyStatistics
            .Where(statistics => statistics.Scores.Any())
            .OrderByDescending(statistics => statistics.DayOfStatistic)
            .ThenByDescending(statistics => statistics.Id)
            .FirstOrDefault();

        if (firstAvailableDate is null || lastAvailableDate is null)
            return language.command_dailyStats_usage;

        return $"{language.command_dailyStats_usage}\n{language.command_dailyStats_validDateRange.Fill([
            firstAvailableDate.DayOfStatistic.ToString(DateFormat, CultureInfo.InvariantCulture),
            lastAvailableDate.DayOfStatistic.ToString(DateFormat, CultureInfo.InvariantCulture)
        ])}";
    }
}
