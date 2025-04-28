using OsuApi.Core.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.OsuTypes;

namespace SosuBot.Extensions
{
    public static class DatabaseTypesExtensions
    {
        public static void Update(this OsuUser userInDatabase, UserExtend user, Playmode playmode)
        {
            userInDatabase.SetPP(user.Statistics!.Pp!.Value, playmode);
            userInDatabase.OsuUsername = user.Username!;
            userInDatabase.OsuUserId = user.Id.Value;
            userInDatabase.OsuMode = user.Playmode!.ParseRulesetToPlaymode();
        }

        public static double GetPP(this OsuUser userInDatabase, Playmode playmode)
        {
            switch (playmode)
            {
                default:
                case Playmode.Osu:
                    return userInDatabase.StdPPValue;
                case Playmode.Taiko:
                    return userInDatabase.TaikoPPValue;
                case Playmode.Catch:
                    return userInDatabase.CatchPPValue;
                case Playmode.Mania:
                    return userInDatabase.ManiaPPValue;
            }
        }

        public static void SetPP(this OsuUser userInDatabase, double pp, Playmode playmode)
        {
            switch (playmode)
            {
                case Playmode.Osu:
                    userInDatabase.StdPPValue = pp;
                    break;
                case Playmode.Taiko:
                    userInDatabase.TaikoPPValue = pp;
                    break;
                case Playmode.Catch:
                    userInDatabase.CatchPPValue = pp;
                    break;
                case Playmode.Mania:
                    userInDatabase.ManiaPPValue = pp;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
