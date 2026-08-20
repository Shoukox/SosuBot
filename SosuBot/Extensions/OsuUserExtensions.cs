using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;

namespace SosuBot.Extensions;

public static class OsuUserExtensions
{
    public static void Update(this OsuUser userInDatabase, UserExtend user, Playmode playmode)
    {
        if (user.Statistics?.Pp is { } pp)
            userInDatabase.SetPP(pp, playmode);

        if (user.Statistics?.HitAccuracy is { } accuracy)
            userInDatabase.SetAccuracy(Convert.ToDouble(accuracy), playmode);

        if (user.Statistics?.PlayTime is { } playTime)
            userInDatabase.SetPlayTime(Convert.ToInt64(playTime), playmode);

        userInDatabase.OsuUsername = user.Username!;
        userInDatabase.OsuUserId = user.Id.Value;
        userInDatabase.OsuMode = playmode;
    }

    // ReSharper disable once InconsistentNaming
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

    // ReSharper disable once InconsistentNaming
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

    public static double? GetAccuracy(this OsuUser userInDatabase, Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => userInDatabase.StdAccuracyValue,
            Playmode.Taiko => userInDatabase.TaikoAccuracyValue,
            Playmode.Catch => userInDatabase.CatchAccuracyValue,
            Playmode.Mania => userInDatabase.ManiaAccuracyValue,
            _ => throw new NotImplementedException()
        };
    }

    public static void SetAccuracy(this OsuUser userInDatabase, double accuracy, Playmode playmode)
    {
        switch (playmode)
        {
            case Playmode.Osu:
                userInDatabase.StdAccuracyValue = accuracy;
                break;
            case Playmode.Taiko:
                userInDatabase.TaikoAccuracyValue = accuracy;
                break;
            case Playmode.Catch:
                userInDatabase.CatchAccuracyValue = accuracy;
                break;
            case Playmode.Mania:
                userInDatabase.ManiaAccuracyValue = accuracy;
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public static long? GetPlayTime(this OsuUser userInDatabase, Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => userInDatabase.StdPlayTimeValue,
            Playmode.Taiko => userInDatabase.TaikoPlayTimeValue,
            Playmode.Catch => userInDatabase.CatchPlayTimeValue,
            Playmode.Mania => userInDatabase.ManiaPlayTimeValue,
            _ => throw new NotImplementedException()
        };
    }

    public static void SetPlayTime(this OsuUser userInDatabase, long playTime, Playmode playmode)
    {
        switch (playmode)
        {
            case Playmode.Osu:
                userInDatabase.StdPlayTimeValue = playTime;
                break;
            case Playmode.Taiko:
                userInDatabase.TaikoPlayTimeValue = playTime;
                break;
            case Playmode.Catch:
                userInDatabase.CatchPlayTimeValue = playTime;
                break;
            case Playmode.Mania:
                userInDatabase.ManiaPlayTimeValue = playTime;
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
