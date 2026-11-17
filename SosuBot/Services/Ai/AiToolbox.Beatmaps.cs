using System.Text.Json;
using System.Text.Json.Nodes;
using SosuBot.Database.Models;
using SosuBot.Extensions;

namespace SosuBot.Services.Ai;

// Beatmapsets, beatmap leaderboards and what a player did recently.
public sealed partial class AiToolbox
{
    private const string OsuSite = "https://osu.ppy.sh";

    private async Task<object> GetBeatmapsetAsync(JsonElement args, CancellationToken cancellationToken)
    {
        long? setId = ReadInt(args, "beatmapset_id");
        if (setId is null && ReadInt(args, "beatmap_id") is { } beatmapId)
            setId = (await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi))?.BeatmapsetId;
        if (setId is null)
            return new { error = "give a beatmapset_id or a beatmap_id" };

        JsonObject? set = await ApiAsync<JsonObject>($"/beatmapsets/{setId}", cancellationToken);
        if (set is null)
            return new { error = "beatmapset not found" };

        Dictionary<long, string> people = Items(set["related_users"])
            .Where(user => Long(user, "id") is not null && Str(user, "username") is not null)
            .GroupBy(user => Long(user, "id")!.Value)
            .ToDictionary(group => group.Key, group => Str(group.First(), "username")!);
        string? creator = Str(set, "creator");

        return new
        {
            Title = $"{Str(set, "artist")} - {Str(set, "title")}",
            Mapper = creator,
            Status = Str(set, "status"),
            Submitted = Date(set, "submitted_date"),
            Ranked = Date(set, "ranked_date"),
            Plays = Long(set, "play_count"),
            Favourites = Long(set, "favourite_count"),
            Bpm = Dbl(set, "bpm", 0),
            Genre = Str(set["genre"], "name"),
            Language = Str(set["language"], "name"),
            Source = Str(set, "source") is { Length: > 0 } source ? source : null,
            Tags = Str(set, "tags") is { Length: > 0 } tags ? AiText.Truncate(tags, 300) : null,
            Nominators = Items(set["current_nominations"])
                .Select(nomination => Long(nomination, "user_id") is { } id && people.TryGetValue(id, out string? name)
                    ? name
                    : null)
                .OfType<string>()
                .Distinct()
                .ToArray(),
            Video = Bool(set, "video") == true ? true : (bool?)null,
            Storyboard = Bool(set, "storyboard") == true ? true : (bool?)null,
            Description = Str(set["description"], "description") is { Length: > 0 } description
                ? AiText.Truncate(AiText.HtmlToText(description), 500)
                : null,
            Difficulties = Items(set["beatmaps"])
                .OrderBy(beatmap => Long(beatmap, "mode_int"))
                .ThenBy(beatmap => Dbl(beatmap, "difficulty_rating"))
                .Take(20)
                .Select(beatmap => new
                {
                    Version = Str(beatmap, "version"),
                    Mode = Str(beatmap, "mode"),
                    Stars = Dbl(beatmap, "difficulty_rating"),
                    LengthSeconds = Long(beatmap, "total_length"),
                    MaxCombo = Long(beatmap, "max_combo"),
                    Mapper = GuestMapper(beatmap, creator),
                    Link = $"{OsuSite}/b/{Long(beatmap, "id")}"
                })
                .ToArray(),
            Link = $"{OsuSite}/beatmapsets/{setId}"
        };
    }

    private async Task<object> GetBeatmapLeaderboardAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int beatmapId = ReadInt(args, "beatmap_id") ?? 0;
        int limit = ReadInt(args, "limit", 1, 10, 5);
        Playmode? mode = ReadMode(args, "mode");
        JsonObject? response = await ApiAsync<JsonObject>(
            $"/beatmaps/{beatmapId}/scores?limit={limit}{(mode is { } m ? $"&mode={m.ToRuleset()}" : "")}",
            cancellationToken);
        if (response is null)
            return new { error = "beatmap not found" };

        JsonNode[] scores = Items(response["scores"]).Take(limit).ToArray();
        return new
        {
            Beatmap = await BeatmapTitleAsync(beatmapId),
            Scores = scores.Select((score, index) => new { Place = index + 1, Score = DescribeRawScore(score) }).ToArray(),
            Note = scores.Length == 0 ? "no scores, the map is probably unranked (graveyard/WIP have no leaderboard)" : null
        };
    }

    private async Task<object> GetUserBeatmapScoreAsync(JsonElement args, CancellationToken cancellationToken)
    {
        Playmode? mode = ReadMode(args, "mode");
        if (await FindUserAsync(ReadString(args, "user"), mode) is not { Id: { } userId } user)
            return new { error = "player not found" };

        int beatmapId = ReadInt(args, "beatmap_id") ?? 0;
        JsonObject? response = await ApiAsync<JsonObject>(
            $"/beatmaps/{beatmapId}/scores/users/{userId}{(mode is { } m ? $"?mode={m.ToRuleset()}" : "")}",
            cancellationToken);
        string? title = await BeatmapTitleAsync(beatmapId);
        if (response?["score"] is not { } score)
            return new { error = $"{user.Username} has no score on this beatmap", Beatmap = title };

        return new
        {
            Player = user.Username,
            Beatmap = title,
            LeaderboardPlace = Long(response, "position"),
            Score = DescribeRawScore(score)
        };
    }

    private async Task<object> GetUserActivityAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (await FindUserAsync(ReadString(args, "user"), null) is not { Id: { } userId } user)
            return new { error = "player not found" };

        int limit = ReadInt(args, "limit", 1, 15, 8);
        JsonArray? events = await ApiAsync<JsonArray>($"/users/{userId}/recent_activity?limit={limit}",
            cancellationToken);
        var described = Items(events).Take(limit)
            .Select(item => new { Date = Date(item, "created_at"), Event = DescribeEvent(item) })
            .Where(item => item.Event is not null)
            .ToArray();
        return new
        {
            Player = user.Username,
            Events = described,
            Note = described.Length == 0 ? "no recent activity (the API keeps only the last 30 days)" : null
        };
    }

    /// <summary>A leaderboard score as the API returns it, in the lazer or the legacy shape.</summary>
    private static object DescribeRawScore(JsonNode score)
    {
        JsonNode? statistics = score["statistics"];
        string mods = string.Concat(Items(score["mods"])
            .Select(mod => mod is JsonValue value && value.TryGetValue(out string? acronym) ? acronym : Str(mod, "acronym"))
            .Where(acronym => acronym is not null and not "CL"));
        return new
        {
            Player = Str(score["user"], "username"),
            Country = Str(score["user"], "country_code"),
            Mods = mods.Length == 0 ? "NM" : mods,
            Accuracy = Dbl(score, "accuracy", 4) is { } accuracy ? Math.Round(accuracy * 100, 2) : (double?)null,
            Combo = Long(score, "max_combo"),
            FullCombo = Bool(score, "is_perfect_combo") ?? Bool(score, "legacy_perfect") ?? Bool(score, "perfect"),
            Misses = Long(statistics, "miss") ?? Long(statistics, "count_miss") ?? 0,
            Rank = Str(score, "rank"),
            Pp = Dbl(score, "pp", 1),
            Date = Date(score, "ended_at") ?? Date(score, "created_at"),
            Link = Long(score, "id") is { } id ? $"{OsuSite}/scores/{id}" : null
        };
    }

    /// <summary>One line about a recent-activity event; <c>null</c> for kinds not worth telling.</summary>
    private static string? DescribeEvent(JsonNode item)
    {
        string? beatmap = Str(item["beatmap"], "title");
        string? beatmapLink = Str(item["beatmap"], "url") is { } url ? OsuSite + url : null;
        string? beatmapset = Str(item["beatmapset"], "title");
        string onMap = $"{beatmap} {beatmapLink}".Trim();
        return Str(item, "type") switch
        {
            "rank" => $"got #{Long(item, "rank")} ({Str(item, "scoreRank")}) in {Str(item, "mode")} on {onMap}",
            "rankLost" => $"lost #1 in {Str(item, "mode")} on {onMap}",
            "achievement" => $"unlocked the medal «{Str(item["achievement"], "name")}»",
            "beatmapsetApprove" => $"beatmapset {beatmapset} became {Str(item, "approval")}",
            "beatmapsetUpload" => $"uploaded the beatmapset {beatmapset}",
            "beatmapsetUpdate" => $"updated the beatmapset {beatmapset}",
            "beatmapsetRevive" => $"revived the beatmapset {beatmapset}",
            "beatmapsetDelete" => $"deleted the beatmapset {beatmapset}",
            "beatmapPlaycount" => $"played {beatmap} {Long(item, "count")} times",
            "usernameChange" => $"changed name from {Str(item["user"], "previousUsername")}",
            "userSupportFirst" => "became an osu!supporter",
            "userSupportAgain" => "bought osu!supporter again",
            "userSupportGift" => "got osu!supporter as a gift",
            _ => null
        };
    }

    private static string? GuestMapper(JsonNode beatmap, string? creator)
    {
        string[] owners = Items(beatmap["owners"]).Select(owner => Str(owner, "username")).OfType<string>().ToArray();
        return owners.Length > 0 && !owners.Contains(creator, StringComparer.OrdinalIgnoreCase)
            ? string.Join(", ", owners)
            : null;
    }

    private async Task<string?> BeatmapTitleAsync(int beatmapId)
    {
        if (await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi) is not { } beatmap)
            return null;

        var set = beatmap.BeatmapsetId is { } setId ? await cachingHelper.GetOrCacheBeatmapset(setId, osuApi) : null;
        return $"{set?.Artist} - {set?.Title} [{beatmap.Version}] {OsuSite}/b/{beatmapId}";
    }
}
