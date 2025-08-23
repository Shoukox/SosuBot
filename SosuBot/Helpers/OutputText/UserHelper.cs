using Microsoft.EntityFrameworkCore;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers.Types;

namespace SosuBot.Helpers.OutputText;

public static class UserHelper
{
    public static async Task<string> GetPpDifferenceTextAsync(BotContext database, UserExtend user, Playmode playmode,
        double? currentPp)
    {
        var ppDifferenceText = string.Empty;
        if (await database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == user.Username) is { } userInDatabase)
        {
            double savedPpInDatabase = userInDatabase.GetPP(playmode);

            var difference = currentPp!.Value - savedPpInDatabase;
            ppDifferenceText = difference.ToString("(+0.00);(-#.##)");
        }

        return ppDifferenceText;
    }

    public static void UpdateOsuUsers(BotContext database, UserExtend user, Playmode playmode)
    {
        foreach (var osuUser in database.OsuUsers.Where(u => u.OsuUsername == user.Username))
        {
            osuUser.Update(user, playmode);
        } 
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
}