using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;

namespace SosuBot.Helpers.OutputText;

/// <summary>
///     Builds the /userw Telegram widget (https://core.telegram.org/bots/widgets). The HTML in
///     <c>Widgets/user-profile.html</c> is fixed code; everything about the player travels in <c>public_data</c>.
/// </summary>
public static class UserProfileWidget
{
    /// <summary>Telegram refuses widget state above this many UTF-8 bytes.</summary>
    public const int MaxDataBytes = 1024;

    private const string ResourceSuffix = "Widgets.user-profile.html";
    private const string LabelsPlaceholder = "__L__";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Labels go into a <script> block and are bot-controlled; keeping UTF-8 keeps the widget small.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Lazy<string> Template = new(LoadTemplate);

    /// <summary>Tab order inside the widget; must match the tab labels in the HTML.</summary>
    public static readonly Playmode[] Modes = [Playmode.Osu, Playmode.Taiko, Playmode.Catch, Playmode.Mania];

    public static string BuildHtml(ILocalization language)
    {
        var labels = new
        {
            rank = language.userWidget_rank,
            acc = language.userWidget_accuracy,
            pc = language.userWidget_playCount,
            pt = language.userWidget_playTime,
            reg = language.userWidget_registered,
            ach = language.userWidget_achievements,
            mc = language.userWidget_maxCombo,
            open = language.userWidget_openProfile,
            h = language.userWidget_hoursShort
        };
        return Template.Value.Replace(LabelsPlaceholder, JsonSerializer.Serialize(labels, JsonOptions));
    }

    /// <param name="statisticsByMode">Statistics indexed like <see cref="Modes" />; <c>null</c> disables a tab.</param>
    public static string BuildPublicData(
        UserExtend user,
        Playmode primaryMode,
        IReadOnlyList<UserStatistics?> statisticsByMode,
        UserProfileDifferenceTexts differences)
    {
        DateTime.TryParse(user.JoinDate?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal,
            out DateTime registeredAt);
        int achievements = user.UserAchievements?.Length ?? 0;

        var data = new Dictionary<string, object?>
        {
            ["n"] = user.Username,
            ["i"] = user.Id,
            ["c"] = user.CountryCode,
            ["m"] = (int)primaryMode,
            ["r"] = registeredAt == default ? null : registeredAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["a"] = $"{achievements}/{OsuConstants.TotalAchievementsCount}",
            ["s"] = statisticsByMode.Select(ToCompactArray).ToArray(),
            ["d"] = new[] { differences.Pp, differences.Accuracy, differences.PlayTime }
        };

        string json = JsonSerializer.Serialize(data, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) <= MaxDataBytes)
            return json;

        // The differences are a nicety; the statistics must fit.
        data["d"] = null;
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>Plain-text summary used as the caption and by clients without widget support.</summary>
    public static string BuildCaption(UserExtend user, Playmode playmode, ScoreHelper scoreHelper) =>
        $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id!.Value, user.Username!)} · " +
        $"{playmode.ToGamemode()} · #{UserHelper.GetUserRankText(user.Statistics?.GlobalRank)} · " +
        $"{scoreHelper.GetFormattedNumConsideringNull(user.Statistics?.Pp, format: "N0")}pp";

    // Order must match the destructuring in user-profile.html.
    private static object?[]? ToCompactArray(UserStatistics? statistics)
    {
        if (statistics is null)
            return null;

        GradeCounts? grades = statistics.GradeCounts;
        return
        [
            statistics.GlobalRank,
            statistics.CountryRank,
            statistics.Pp is { } pp ? Math.Round(pp) : null,
            statistics.HitAccuracy is { } accuracy ? Math.Round(Convert.ToDouble(accuracy), 2) : null,
            statistics.PlayCount,
            statistics.PlayTime is { } playTime ? Convert.ToInt64(playTime) / 3600 : null,
            statistics.Level?.Current,
            statistics.Level?.Progress,
            grades?.SSH,
            grades?.SS,
            grades?.SH,
            grades?.S,
            grades?.A,
            statistics.MaximumCombo
        ];
    }

    private static string LoadTemplate()
    {
        var assembly = typeof(UserProfileWidget).Assembly;
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
