using Microsoft.EntityFrameworkCore;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Helpers.OutputText;

public readonly record struct UserProfileDifferenceTexts(
    string Pp,
    string Accuracy,
    string PlayTime);

public static class UserHelper
{
    public static async Task<UserProfileDifferenceTexts> GetProfileDifferenceTextsAsync(
        BotContext database,
        UserExtend user,
        Playmode playmode)
    {
        if (user.Statistics is null ||
            await database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == user.Username) is not { } userInDatabase)
        {
            return new UserProfileDifferenceTexts(string.Empty, string.Empty, string.Empty);
        }

        double? currentAccuracy = user.Statistics.HitAccuracy is { } accuracy
            ? Convert.ToDouble(accuracy)
            : null;
        long? currentPlayTime = user.Statistics.PlayTime is { } playTime
            ? Convert.ToInt64(playTime)
            : null;

        return new UserProfileDifferenceTexts(
            FormatPpDifference(user.Statistics.Pp, userInDatabase.GetPP(playmode)),
            FormatAccuracyDifference(currentAccuracy, userInDatabase.GetAccuracy(playmode)),
            FormatPlayTimeDifference(currentPlayTime, userInDatabase.GetPlayTime(playmode)));
    }

    public static async Task<string> GetPpDifferenceTextAsync(BotContext database, UserExtend user, Playmode playmode,
        double? currentPp)
    {
        if (await database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == user.Username) is not { } userInDatabase)
            return string.Empty;

        return FormatPpDifference(currentPp, userInDatabase.GetPP(playmode));
    }

    private static string FormatPpDifference(double? currentValue, double savedValue)
    {
        if (!currentValue.HasValue)
            return string.Empty;

        var difference = currentValue.Value - savedValue;
        return difference.ToString("(+0.00);(-#.##)");
    }

    private static string FormatAccuracyDifference(double? currentValue, double? savedValue)
    {
        if (!currentValue.HasValue || !savedValue.HasValue)
            return string.Empty;

        var difference = currentValue.Value - savedValue.Value;
        return $"({difference.ToString("+0.00;-0.00")})";
    }

    private static string FormatPlayTimeDifference(long? currentValue, long? savedValue)
    {
        if (!currentValue.HasValue || !savedValue.HasValue)
            return string.Empty;

        var difference = currentValue.Value - savedValue.Value;
        if (difference == 0)
            return "(+0m)";

        var sign = difference > 0 ? "+" : "-";
        var duration = TimeSpan.FromSeconds(Math.Abs(difference));

        if (duration.TotalHours >= 1)
        {
            var hours = (long)duration.TotalHours;
            return duration.Minutes == 0
                ? $"({sign}{hours}h)"
                : $"({sign}{hours}h {duration.Minutes}m)";
        }

        if (duration.TotalMinutes >= 1)
            return $"({sign}{duration.Minutes}m)";

        return $"({sign}{duration.Seconds}s)";
    }

    public static string CountryCodeToFlag(string countryCode)
    {
        // Ensure uppercase
        countryCode = countryCode.ToUpperInvariant();

        // Regional indicator offset in Unicode
        const int regionalIndicatorOffset = 0x1F1E6 - 'A';

        var flag = string.Concat(
            countryCode.Select(c => char.ConvertFromUtf32(c + regionalIndicatorOffset))
        );

        return flag;
    }

    public static void UpdateOsuUsers(BotContext database, UserExtend user, Playmode playmode)
    {
        foreach (OsuUser? osuUser in database.OsuUsers.Where(u => u.OsuUsername == user.Username))
            osuUser.Update(user, playmode);
    }

    public static string GetUserRankText(int? rank)
    {
        var ppText = rank?.ToString() ?? "—";
        return ppText;
    }

    public static string GetUserProfileUrl(int userId)
    {
        return $"{OsuConstants.BaseUserProfileUrl}{userId}";
    }

    public static string GetUserProfileUrlWrappedInUsernameString(int userId, string username)
    {
        return $"<a href=\"{GetUserProfileUrl(userId)}\">{username}</a>";
    }

    public static InlineKeyboardMarkup BuildUserModeKeyboard(string username)
    {
        return new InlineKeyboardMarkup(new InlineKeyboardButton[][]
        {
            [
                new InlineKeyboardButton("Standard") { CallbackData = $"user 0 {username}" },
                new InlineKeyboardButton("Taiko") { CallbackData = $"user 1 {username}" }
            ],
            [
                new InlineKeyboardButton("Catch") { CallbackData = $"user 2 {username}" },
                new InlineKeyboardButton("Mania") { CallbackData = $"user 3 {username}" }
            ]
        });
    }
}
