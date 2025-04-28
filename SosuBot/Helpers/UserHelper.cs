using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.OsuTypes;

namespace SosuBot.Helpers
{
    public static class UserHelper
    {
        public static async Task<string> GetPPDifferenceTextAsync(BotContext database, UserExtend user, Playmode playmode, double? currentPP, double? savedPPInDatabase)
        {
            string ppDifferenceText = string.Empty;
            if (await database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == user.Username) is OsuUser userInDatabase)
            {
                savedPPInDatabase = userInDatabase!.GetPP(playmode);

                double difference = currentPP!.Value - savedPPInDatabase!.Value;
                ppDifferenceText = difference.ToString("(+0.00);(-#.##)");

                userInDatabase.Update(user, playmode);
            }
            return ppDifferenceText;
        }
    }
}
