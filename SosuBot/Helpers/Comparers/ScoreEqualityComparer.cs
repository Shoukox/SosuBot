using MongoDB.Bson.IO;
using OsuApi.V2.Models;

namespace SosuBot.Helpers.Comparers;

public class ScoreEqualityComparer : EqualityComparer<Score>
{
    public override bool Equals(Score? x, Score? y)
    {
        if(ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return x == y;

        if (x.Id == y.Id && x.StartedAt != y.StartedAt)
        {
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/xScore.txt", Newtonsoft.Json.JsonConvert.SerializeObject(x));
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/yScore.txt", Newtonsoft.Json.JsonConvert.SerializeObject(y));
            throw new Exception("ALARM");
        }

        return x.Id == y.Id
               && x.StartedAt == y.StartedAt
               && x.EndedAt == y.EndedAt;
    }

    public override int GetHashCode(Score obj) => HashCode.Combine(obj.Id, obj.StartedAt, obj.EndedAt);

}