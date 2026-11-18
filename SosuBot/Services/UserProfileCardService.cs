using System.Globalization;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;

namespace SosuBot.Services;

/// <summary>Draws the /userp picture of a player, for the command and for its mode buttons.</summary>
public sealed class UserProfileCardService(OsuProfileImageService images, UserProfileCardGenerator generator)
{
    public async Task<MemoryStream> GenerateAsync(UserExtend user, Playmode playmode,
        UserProfileDifferenceTexts differences, CancellationToken cancellationToken)
    {
        Task<byte[]?> avatar = images.DownloadAvatarAsync(user, cancellationToken);
        Task<byte[]?> banner = images.DownloadBannerAsync(user, cancellationToken);
        return generator.Generate(ToCardData(user, playmode, differences, await avatar, await banner));
    }

    public static UserProfileCardData ToCardData(UserExtend user, Playmode playmode,
        UserProfileDifferenceTexts differences, byte[]? avatar, byte[]? banner)
    {
        UserStatistics? statistics = user.Statistics;
        GradeCounts? grades = statistics?.GradeCounts;
        bool registered = DateTime.TryParse(user.JoinDate?.Value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal, out DateTime registeredAt);

        return new UserProfileCardData
        {
            Username = user.Username ?? "?",
            CountryCode = user.CountryCode,
            Mode = (OsuGameMode)playmode,
            Avatar = avatar,
            Banner = banner,
            GlobalRank = statistics?.GlobalRank,
            CountryRank = statistics?.CountryRank,
            Pp = statistics?.Pp,
            AccuracyPercent = statistics?.HitAccuracy,
            PlayCount = statistics?.PlayCount,
            PlayTimeHours = statistics?.PlayTime / 3600,
            MaximumCombo = statistics?.MaximumCombo,
            Level = statistics?.Level?.Current,
            LevelProgress = statistics?.Level?.Progress,
            PpDifference = differences.Pp,
            AccuracyDifference = differences.Accuracy,
            PlayTimeDifference = differences.PlayTime,
            SilverSs = grades?.SSH,
            Ss = grades?.SS,
            SilverS = grades?.SH,
            S = grades?.S,
            A = grades?.A,
            Registered = registered ? registeredAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : null,
            Achievements = $"{user.UserAchievements?.Length ?? 0}/{OsuConstants.TotalAchievementsCount}"
        };
    }
}
