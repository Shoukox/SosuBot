using MongoDB.Bson.IO;
using OsuApi.V2.Models;

namespace SosuBot.Helpers.Comparers;

public class ScoreEqualityComparer : EqualityComparer<Score>
{
    public override bool Equals(Score? x, Score? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        if (x.Id == y.Id && x.MaxCombo != y.MaxCombo)
        {
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/xScore.txt", Newtonsoft.Json.JsonConvert.SerializeObject(x));
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/yScore.txt", Newtonsoft.Json.JsonConvert.SerializeObject(y));
            throw new Exception("ALARM");
        }

        return x.Id == y.Id
               && x.StartedAt == y.StartedAt
               && x.EndedAt == y.EndedAt
               && x.MaxCombo == y.MaxCombo;
    }

    public override int GetHashCode(Score obj) => obj.Id.GetHashCode();

}