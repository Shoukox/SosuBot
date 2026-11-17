using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using osu.Game.Rulesets.Scoring;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.PerformanceCalculator;
using GameMod = osu.Game.Rulesets.Mods.Mod;

#pragma warning disable OPENAI001

namespace SosuBot.Services.Ai;

/// <summary>What a tool call may do in the chat it was made from.</summary>
public sealed record AiToolContext(Func<string, Task<bool>> ReactAsync);

/// <summary>
/// The osu! tools the AI can call. Every result is compact JSON with only what a reply needs, so tool output
/// stays cheap in tokens. Errors come back as <c>{"error": ...}</c> for the model to explain in its own words.
/// </summary>
public sealed class AiToolbox(
    BanchoApiV2 osuApi,
    CachingHelper cachingHelper,
    BeatmapsService beatmapsService,
    IPerformanceCalculator performanceCalculator,
    ILogger<AiToolbox> logger)
{
    /// <summary>Reactions a bot may set without Premium; the ones that fit a playful persona.</summary>
    public static readonly string[] Reactions =
    [
        "❤", "🥰", "😍", "😘", "😁", "🤣", "😭", "🙈", "🤔", "👀", "🔥", "💯", "🏆", "👏", "😎", "🤗", "😇",
        "🤯", "😱", "🥱", "🫡", "💅", "🤡", "🗿", "🌚", "😐", "😢", "🎉"
    ];

    private const int MaxSearchResults = 8;
    private const int MaxScores = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public IReadOnlyList<ResponseTool> Tools { get; } =
    [
        Tool("get_osu_user",
            "osu! profile of a player: pp, ranks, accuracy, play count and time, grade counts.",
            """
            {
              "type": "object",
              "properties": {
                "user": { "type": "string", "description": "osu! username or numeric user id" },
                "mode": { "type": ["integer", "null"], "description": "0 std, 1 taiko, 2 catch, 3 mania; null for the player's main mode" }
              },
              "required": ["user", "mode"],
              "additionalProperties": false
            }
            """),
        Tool("get_user_scores",
            "A player's scores: best (top plays), recent (plays of the last 24 hours, fails included), firsts (#1 ranks) or pinned.",
            """
            {
              "type": "object",
              "properties": {
                "user": { "type": "string", "description": "osu! username or numeric user id" },
                "type": { "type": "string", "enum": ["best", "recent", "firsts", "pinned"] },
                "mode": { "type": ["integer", "null"], "description": "0 std, 1 taiko, 2 catch, 3 mania; null for the player's main mode" },
                "limit": { "type": "integer", "description": "1-10" }
              },
              "required": ["user", "type", "mode", "limit"],
              "additionalProperties": false
            }
            """),
        Tool("get_beatmap",
            "A beatmap difficulty by id: title, mapper, status, stars, bpm, length, CS/AR/OD/HP, max combo.",
            """
            {
              "type": "object",
              "properties": {
                "beatmap_id": { "type": "integer", "description": "Beatmap (difficulty) id, the number after /b/ or after # in a link" }
              },
              "required": ["beatmap_id"],
              "additionalProperties": false
            }
            """),
        Tool("search_beatmaps",
            "Searches osu! beatmaps, e.g. to recommend maps for a player's level. Filters apply to difficulties.",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "Artist, title, mapper or tags; empty for any" },
                "mode": { "type": "integer", "description": "0 std, 1 taiko, 2 catch, 3 mania" },
                "min_stars": { "type": ["number", "null"] },
                "max_stars": { "type": ["number", "null"] },
                "max_length_seconds": { "type": ["integer", "null"] },
                "min_bpm": { "type": ["number", "null"] },
                "max_bpm": { "type": ["number", "null"] },
                "status": { "type": "string", "enum": ["ranked", "loved", "qualified", "any"] },
                "sort": { "type": "string", "enum": ["popular", "favourites", "newest", "rating"] },
                "limit": { "type": "integer", "description": "1-8" }
              },
              "required": ["query", "mode", "min_stars", "max_stars", "max_length_seconds", "min_bpm", "max_bpm", "status", "sort", "limit"],
              "additionalProperties": false
            }
            """),
        Tool("calculate_pp",
            "Official osu! pp for a play on a beatmap: pp for the given accuracy and misses plus pp for a full combo. Use it to judge scores or answer 'how much pp would X give'.",
            """
            {
              "type": "object",
              "properties": {
                "beatmap_id": { "type": "integer" },
                "mode": { "type": ["integer", "null"], "description": "0 std, 1 taiko, 2 catch, 3 mania; null for the beatmap's own mode" },
                "mods": { "type": "string", "description": "Mod acronyms like HDDT or HR, empty for no mods" },
                "accuracy": { "type": "number", "description": "Accuracy in percent, e.g. 98.5" },
                "misses": { "type": "integer" },
                "combo": { "type": ["integer", "null"], "description": "Max combo of the play; null for a full combo" }
              },
              "required": ["beatmap_id", "mode", "mods", "accuracy", "misses", "combo"],
              "additionalProperties": false
            }
            """),
        Tool("get_country_ranking",
            "Top players of a country by pp.",
            """
            {
              "type": "object",
              "properties": {
                "country_code": { "type": "string", "description": "Two-letter country code, e.g. UZ" },
                "mode": { "type": "integer", "description": "0 std, 1 taiko, 2 catch, 3 mania" },
                "count": { "type": "integer", "description": "1-50" }
              },
              "required": ["country_code", "mode", "count"],
              "additionalProperties": false
            }
            """),
        Tool("react",
            "Puts a reaction on the message you are answering, like a person would. Use it sometimes, not every time.",
            $$"""
            {
              "type": "object",
              "properties": {
                "emoji": { "type": "string", "enum": [{{string.Join(", ", Reactions.Select(r => $"\"{r}\""))}}] }
              },
              "required": ["emoji"],
              "additionalProperties": false
            }
            """)
    ];

    public async Task<string> ExecuteAsync(string name, BinaryData arguments, AiToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            JsonElement args = JsonDocument.Parse(arguments.ToMemory()).RootElement;
            object result = name switch
            {
                "get_osu_user" => await GetOsuUserAsync(args),
                "get_user_scores" => await GetUserScoresAsync(args),
                "get_beatmap" => await GetBeatmapAsync(args),
                "search_beatmaps" => await SearchBeatmapsAsync(args, cancellationToken),
                "calculate_pp" => await CalculatePpAsync(args, cancellationToken),
                "get_country_ranking" => await GetCountryRankingAsync(args, cancellationToken),
                "react" => await context.ReactAsync(args.GetProperty("emoji").GetString() ?? "")
                    ? new { ok = true }
                    : new { error = "reaction not allowed in this chat" },
                _ => new { error = $"unknown tool {name}" }
            };
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI tool {Tool} failed", name);
            return JsonSerializer.Serialize(new { error = "the osu! request failed" }, JsonOptions);
        }
    }

    private static ResponseTool Tool(string name, string description, string schema) =>
        ResponseTool.CreateFunctionTool(name, BinaryData.FromString(schema), strictModeEnabled: true,
            functionDescription: description);

    private async Task<object> GetOsuUserAsync(JsonElement args)
    {
        Playmode? mode = ReadMode(args, "mode");
        if (await FindUserAsync(args.GetProperty("user").GetString(), mode) is not { } user)
            return new { error = "player not found" };

        UserStatistics? s = user.Statistics;
        return new
        {
            user.Username,
            user.Id,
            Country = user.CountryCode,
            Mode = (mode ?? ModeOf(user)).ToGamemode(),
            Pp = Round(s?.Pp, 0),
            GlobalRank = s?.GlobalRank,
            CountryRank = s?.CountryRank,
            Accuracy = Round(s?.HitAccuracy, 2),
            PlayCount = s?.PlayCount,
            PlayTimeHours = s?.PlayTime / 3600,
            Level = s?.Level?.Current,
            MaxCombo = s?.MaximumCombo,
            Grades = s?.GradeCounts is { } g ? new { SSH = g.SSH, SS = g.SS, SH = g.SH, S = g.S, A = g.A } : null,
            HighestRank = user.RankHighest?.Rank,
            Joined = user.JoinDate?.Value?[..Math.Min(10, user.JoinDate.Value.Length)],
            user.Playstyle,
            user.IsSupporter,
            PreviousUsernames = user.PreviousUsernames is { Length: > 0 } names ? names : null,
            Link = $"https://osu.ppy.sh/users/{user.Id}"
        };
    }

    private async Task<object> GetUserScoresAsync(JsonElement args)
    {
        Playmode? requestedMode = ReadMode(args, "mode");
        if (await FindUserAsync(args.GetProperty("user").GetString(), requestedMode) is not { } user)
            return new { error = "player not found" };

        Playmode mode = requestedMode ?? ModeOf(user);
        string type = args.GetProperty("type").GetString() ?? "best";
        int limit = Math.Clamp(args.GetProperty("limit").GetInt32(), 1, MaxScores);
        GetUserScoresResponse? response = await osuApi.Users.GetUserScores(user.Id!.Value, type,
            new GetUserScoreQueryParameters
            {
                Mode = mode.ToRuleset(),
                Limit = limit,
                IncludeFails = type == "recent" ? 1 : null
            });

        return new
        {
            Player = user.Username,
            Mode = mode.ToGamemode(),
            Type = type,
            Scores = (response?.Scores ?? []).Select(DescribeScore).ToArray()
        };
    }

    private static object DescribeScore(Score score) => new
    {
        Beatmap = $"{score.Beatmapset?.Artist} - {score.Beatmapset?.Title} [{score.Beatmap?.Version}]",
        BeatmapId = score.BeatmapId,
        Stars = Round(score.Beatmap?.DifficultyRating, 2),
        Status = score.Beatmap?.Status,
        Mods = score.Mods is { Length: > 0 } mods ? string.Concat(mods.Select(m => m.Acronym)) : "NM",
        Accuracy = Round(score.Accuracy * 100, 2),
        Combo = $"{score.MaxCombo}/{score.Beatmap?.MaxCombo}",
        Rank = score.Passed == false ? "F" : score.Rank,
        Pp = Round(score.Pp, 1),
        Misses = score.Statistics?.Miss,
        Hits = score.Statistics is { } st ? $"{st.Great}/{st.Ok}/{st.Meh}" : null,
        Date = score.EndedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        Link = score.Id is { } id ? $"https://osu.ppy.sh/scores/{id}" : null
    };

    private async Task<object> GetBeatmapAsync(JsonElement args)
    {
        int beatmapId = args.GetProperty("beatmap_id").GetInt32();
        if (await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi) is not { } beatmap)
            return new { error = "beatmap not found" };

        BeatmapsetExtended? set = beatmap.BeatmapsetId is { } setId
            ? await cachingHelper.GetOrCacheBeatmapset(setId, osuApi)
            : null;
        return new
        {
            Title = $"{set?.Artist} - {set?.Title} [{beatmap.Version}]",
            Mapper = set?.Creator,
            beatmap.Status,
            Mode = beatmap.Mode,
            Stars = Round(beatmap.DifficultyRating, 2),
            Bpm = Round(beatmap.BPM, 0),
            LengthSeconds = beatmap.TotalLength,
            Cs = beatmap.CS,
            Ar = beatmap.AR,
            Od = beatmap.Accuracy,
            Hp = beatmap.Drain,
            beatmap.MaxCombo,
            beatmap.Playcount,
            Link = $"https://osu.ppy.sh/b/{beatmap.Id}"
        };
    }

    private async Task<object> SearchBeatmapsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int mode = Math.Clamp(args.GetProperty("mode").GetInt32(), 0, 3);
        double? minStars = ReadDouble(args, "min_stars");
        double? maxStars = ReadDouble(args, "max_stars");
        int? maxLength = args.GetProperty("max_length_seconds").ValueKind == JsonValueKind.Number
            ? args.GetProperty("max_length_seconds").GetInt32()
            : null;
        double? minBpm = ReadDouble(args, "min_bpm");
        double? maxBpm = ReadDouble(args, "max_bpm");
        int limit = Math.Clamp(args.GetProperty("limit").GetInt32(), 1, MaxSearchResults);

        // osu!'s own search syntax: free text plus key<value filters.
        List<string> query = [args.GetProperty("query").GetString()?.Trim() ?? ""];
        if (minStars is { } a) query.Add($"stars>={F(a)}");
        if (maxStars is { } b) query.Add($"stars<={F(b)}");
        if (maxLength is { } l) query.Add($"length<={l}");
        if (minBpm is { } c) query.Add($"bpm>={F(c)}");
        if (maxBpm is { } d) query.Add($"bpm<={F(d)}");

        string status = args.GetProperty("status").GetString() ?? "ranked";
        string sort = (args.GetProperty("sort").GetString() ?? "popular") switch
        {
            "favourites" => "favourites_desc",
            "newest" => "ranked_desc",
            "rating" => "rating_desc",
            _ => "plays_desc"
        };
        string url = $"{BanchoApiV2.ApiMainFunctionsBaseAddress.TrimEnd('/')}/beatmapsets/search" +
                     $"?q={Uri.EscapeDataString(string.Join(' ', query).Trim())}&m={mode}" +
                     $"&s={(status == "any" ? "any" : status)}&sort={sort}";

        JsonObject? response = await osuApi.MakeRequestAsync<JsonObject>(url, HttpMethod.Get,
            cancellationToken: cancellationToken);
        var results = new List<object>();
        foreach (JsonNode? set in response?["beatmapsets"]?.AsArray() ?? [])
        {
            if (set is null) continue;
            var difficulties = (set["beatmaps"]?.AsArray() ?? [])
                .Where(b => b?["mode_int"]?.GetValue<int>() == mode)
                .Select(b => new
                {
                    Id = b!["id"]!.GetValue<int>(),
                    Version = b["version"]?.GetValue<string>(),
                    Stars = Math.Round(b["difficulty_rating"]?.GetValue<double>() ?? 0, 2),
                    LengthSeconds = b["total_length"]?.GetValue<int>(),
                    Bpm = b["bpm"]?.GetValue<double>()
                })
                .Where(b => (minStars is null || b.Stars >= minStars) && (maxStars is null || b.Stars <= maxStars))
                .OrderBy(b => b.Stars)
                .Take(3)
                .Select(b => new
                {
                    b.Version, b.Stars, b.LengthSeconds, b.Bpm, Link = $"https://osu.ppy.sh/b/{b.Id}"
                })
                .ToArray();
            if (difficulties.Length == 0) continue;

            results.Add(new
            {
                Title = $"{set["artist"]?.GetValue<string>()} - {set["title"]?.GetValue<string>()}",
                Mapper = set["creator"]?.GetValue<string>(),
                Status = set["status"]?.GetValue<string>(),
                Plays = set["play_count"]?.GetValue<int>(),
                Favourites = set["favourite_count"]?.GetValue<int>(),
                Difficulties = difficulties
            });
            if (results.Count == limit) break;
        }

        return results.Count == 0 ? new { error = "nothing found, try wider filters" } : new { Results = results };
    }

    private async Task<object> CalculatePpAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int beatmapId = args.GetProperty("beatmap_id").GetInt32();
        if (await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi) is not { } beatmap)
            return new { error = "beatmap not found" };
        if (beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners >= 20_000)
            return new { error = "the beatmap is too long to calculate" };

        Playmode mode = ReadMode(args, "mode") ?? beatmap.Mode?.ParseRulesetToPlaymode() ?? Playmode.Osu;
        string modsText = (args.GetProperty("mods").GetString() ?? "").Replace("+", "").Replace(" ", "")
            .Replace(",", "");
        if (modsText.Equals("NM", StringComparison.OrdinalIgnoreCase)) modsText = "";
        if (!modsText.TryParseMods(mode, out GameMod[] mods))
            return new { error = $"unknown mods {modsText}" };

        double accuracy = Math.Clamp(args.GetProperty("accuracy").GetDouble(), 1, 100) / 100;
        int misses = Math.Max(0, args.GetProperty("misses").GetInt32());
        int? combo = args.GetProperty("combo").ValueKind == JsonValueKind.Number
            ? args.GetProperty("combo").GetInt32()
            : null;

        await using Stream file = await beatmapsService.DownloadOrCacheBeatmapAsync(beatmapId, beatmap.Checksum,
            cancellationToken);
        PPCalculationResult? play = await performanceCalculator.CalculatePpAsync(beatmapId, file, accuracy,
            scoreMaxCombo: combo, scoreMods: mods,
            scoreStatistics: misses > 0 ? new Dictionary<HitResult, int> { [HitResult.Miss] = misses } : null,
            rulesetId: (int)mode, cancellationToken: cancellationToken);
        file.Position = 0;
        PPCalculationResult? fullCombo = await performanceCalculator.CalculatePpAsync(beatmapId, file, accuracy,
            scoreMods: mods, rulesetId: (int)mode, cancellationToken: cancellationToken);

        if (play is null || fullCombo is null)
            return new { error = "could not calculate this play" };

        return new
        {
            Mode = mode.ToGamemode(),
            Mods = mods.Length == 0 ? "NM" : string.Concat(mods.Select(m => m.Acronym)),
            Accuracy = Math.Round(play.CalculatedAccuracy * 100, 2),
            Misses = misses,
            Combo = $"{combo ?? play.BeatmapMaxCombo}/{play.BeatmapMaxCombo}",
            Pp = Math.Round(play.PP, 1),
            FullComboPp = Math.Round(fullCombo.PP, 1),
            Stars = Math.Round(play.DifficultyAttributes.StarRating, 2)
        };
    }

    private async Task<object> GetCountryRankingAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string country = (args.GetProperty("country_code").GetString() ?? "uz").Trim().ToLowerInvariant();
        var mode = (Playmode)Math.Clamp(args.GetProperty("mode").GetInt32(), 0, 3);
        int count = Math.Clamp(args.GetProperty("count").GetInt32(), 1, 50);
        List<UserStatistics>? ranking = await OsuApiHelper.GetUsersFromRanking(osuApi, mode, country, count,
            cancellationToken);
        return new
        {
            Country = country.ToUpperInvariant(),
            Mode = mode.ToGamemode(),
            Players = (ranking ?? []).Take(count).Select((s, index) => new
            {
                Place = index + 1,
                s.User?.Username,
                Pp = Round(s.Pp, 0),
                Accuracy = Round(s.HitAccuracy, 2),
                s.PlayCount
            }).ToArray()
        };
    }

    private async Task<UserExtend?> FindUserAsync(string? user, Playmode? mode)
    {
        user = user?.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(user))
            return null;

        GetUserResponse? response = await osuApi.Users.GetUser(
            long.TryParse(user, out _) ? user : $"@{user}",
            new GetUserQueryParameters(),
            mode?.ToRuleset());
        return response?.UserExtend;
    }

    private static Playmode ModeOf(UserExtend user) =>
        user.Playmode?.ParseRulesetToPlaymode() ?? Playmode.Osu;

    private static Playmode? ReadMode(JsonElement args, string property) =>
        args.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? (Playmode)Math.Clamp(value.GetInt32(), 0, 3)
            : null;

    private static double? ReadDouble(JsonElement args, string property) =>
        args.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static double? Round(float? value, int digits) => value is { } v ? Math.Round(v, digits) : null;

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
