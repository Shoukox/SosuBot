﻿using Microsoft.EntityFrameworkCore;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.Types;

namespace SosuBot.Helpers.OutputText;

public static class UserHelper
{
    public static async Task<string> GetPPDifferenceTextAsync(BotContext database, UserExtend user, Playmode playmode,
        double? currentPP, double? savedPPInDatabase)
    {
        var ppDifferenceText = string.Empty;
        if (await database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == user.Username) is OsuUser userInDatabase)
        {
            savedPPInDatabase = userInDatabase!.GetPP(playmode);

            var difference = currentPP!.Value - savedPPInDatabase!.Value;
            ppDifferenceText = difference.ToString("(+0.00);(-#.##)");

            userInDatabase.Update(user, playmode);
        }

        return ppDifferenceText;
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