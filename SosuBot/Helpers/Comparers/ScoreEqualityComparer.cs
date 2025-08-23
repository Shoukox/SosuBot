using OsuApi.V2.Models;

namespace SosuBot.Helpers.Comparers;

public class ScoreEqualityComparer : EqualityComparer<Score>
{
    public override bool Equals(Score? x, Score? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return x == y;

        return x.Id == y.Id
               && x.StartedAt == y.StartedAt
               && x.EndedAt == y.EndedAt;
    }

    public override int GetHashCode(Score obj)
    {
        return HashCode.Combine(obj.Id, obj.StartedAt, obj.EndedAt);
    }
}