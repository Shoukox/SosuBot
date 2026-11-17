using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
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

/// <summary>The chat a tool call was made from and what it may do there.</summary>
/// <param name="ChatId">The Telegram chat the AI answers in.</param>
/// <param name="PrivateChat">The chat is the private chat with the speaker.</param>
/// <param name="SpeakerId">Telegram id of the person the AI answers.</param>
public sealed record AiToolContext(long ChatId, bool PrivateChat, long SpeakerId, Func<string, Task<bool>> ReactAsync);

/// <summary>A tool the model may call.</summary>
/// <param name="Schema">Strict JSON schema of its arguments: every property is required, nullable when optional.</param>
public sealed record AiToolDefinition(string Name, string Description, string Schema);

/// <summary>
/// The osu! and bot tools the AI can call. Every result is compact JSON with only what a reply needs, so tool
/// output stays cheap in tokens. Errors come back as <c>{"error": ...}</c> for the model to explain in its own words.
/// </summary>
public sealed partial class AiToolbox(
    BanchoApiV2 osuApi,
    CachingHelper cachingHelper,
    BeatmapsService beatmapsService,
    IPerformanceCalculator performanceCalculator,
    IServiceScopeFactory scopeFactory,
    TelegramUserDirectory telegramUsers,
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

    /// <summary>Every tool the model may call: its name, what it is for and its strict JSON schema.</summary>
    public static readonly IReadOnlyList<AiToolDefinition> Definitions =
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
        Tool("get_ranking",
            "osu! rankings, global or of one country: players by pp (performance) or by ranked score (score), or countries by total pp (country). Use it for 'who is #1', 'best players of Uzbekistan', 'strongest country'.",
            """
            {
              "type": "object",
              "properties": {
                "type": { "type": "string", "enum": ["performance", "score", "country"] },
                "mode": { "type": "integer", "description": "0 std, 1 taiko, 2 catch, 3 mania" },
                "country_code": { "type": ["string", "null"], "description": "Two-letter country code like UZ for one country; null for the whole world. Ignored for type country" },
                "count": { "type": "integer", "description": "1-50" }
              },
              "required": ["type", "mode", "country_code", "count"],
              "additionalProperties": false
            }
            """),
        Tool("search_wiki",
            "Searches the official osu! wiki: game mechanics, mods, skills and terms (finger control, aim, streams, jumps, reading), history, tournaments, famous players and maps, rules. Returns page paths for get_wiki_page.",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "Search words, best in English, e.g. 'hit window', 'Hidden mod', 'osu! World Cup'" },
                "locale": { "type": "string", "description": "Language of the conversation: ru, en, de, ...; uz and unknown languages use en" }
              },
              "required": ["query", "locale"],
              "additionalProperties": false
            }
            """),
        Tool("get_wiki_page",
            "Reads an osu! wiki page as plain text. Without a section you get the outline and the beginning; ask for a section to read just that part. Falls back to English when there is no translation.",
            """
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "Wiki path from search_wiki or a wiki link, e.g. Gameplay/Game_modifier/Hidden" },
                "locale": { "type": "string", "description": "ru, en, de, ..." },
                "section": { "type": ["string", "null"], "description": "A heading from the outline; null for the outline and the beginning" }
              },
              "required": ["path", "locale", "section"],
              "additionalProperties": false
            }
            """),
        Tool("get_news",
            "Latest posts of the official osu! news: tournaments, updates, events, featured artists, project news.",
            """
            {
              "type": "object",
              "properties": {
                "limit": { "type": "integer", "description": "1-8" }
              },
              "required": ["limit"],
              "additionalProperties": false
            }
            """),
        Tool("get_news_post",
            "The text of one osu! news post, by the slug from get_news.",
            """
            {
              "type": "object",
              "properties": {
                "slug": { "type": "string", "description": "Post slug, e.g. 2026-11-01-osu-world-cup-2026-results" }
              },
              "required": ["slug"],
              "additionalProperties": false
            }
            """),
        Tool("get_changelog",
            "Recent osu! releases and what changed in them: lazer (the current client), stable (osu! classic), cuttingedge (stable testing) or web (the website).",
            """
            {
              "type": "object",
              "properties": {
                "stream": { "type": "string", "enum": ["lazer", "stable", "cuttingedge", "web"] },
                "limit": { "type": "integer", "description": "Number of releases, 1-5" }
              },
              "required": ["stream", "limit"],
              "additionalProperties": false
            }
            """),
        Tool("get_spotlights",
            "osu! spotlights (seasonal and themed ranking events with their own map pools). Without an id: the latest spotlights; with an id: the top players of that spotlight by ranked score on its maps.",
            """
            {
              "type": "object",
              "properties": {
                "mode": { "type": "integer", "description": "0 std, 1 taiko, 2 catch, 3 mania" },
                "spotlight_id": { "type": ["integer", "null"], "description": "Id from the list; null for the list" }
              },
              "required": ["mode", "spotlight_id"],
              "additionalProperties": false
            }
            """),
        Tool("get_beatmapset",
            "A whole beatmapset: artist, title, mapper, status and ranked date, plays, favourites, genre, language, tags, nominators, all difficulties. Give one of the ids.",
            """
            {
              "type": "object",
              "properties": {
                "beatmapset_id": { "type": ["integer", "null"], "description": "Set id, the number after /beatmapsets/ or /s/" },
                "beatmap_id": { "type": ["integer", "null"], "description": "Any difficulty id of the set, the number after /b/ or after #" }
              },
              "required": ["beatmapset_id", "beatmap_id"],
              "additionalProperties": false
            }
            """),
        Tool("get_beatmap_leaderboard",
            "Global top scores on a beatmap difficulty: who is #1, with what mods, accuracy and pp.",
            """
            {
              "type": "object",
              "properties": {
                "beatmap_id": { "type": "integer" },
                "mode": { "type": ["integer", "null"], "description": "0 std, 1 taiko, 2 catch, 3 mania for converts; null for the beatmap's own mode" },
                "limit": { "type": "integer", "description": "1-10" }
              },
              "required": ["beatmap_id", "mode", "limit"],
              "additionalProperties": false
            }
            """),
        Tool("get_user_beatmap_score",
            "A player's best score on one beatmap and its place on the global leaderboard.",
            """
            {
              "type": "object",
              "properties": {
                "user": { "type": "string", "description": "osu! username or numeric user id" },
                "beatmap_id": { "type": "integer" },
                "mode": { "type": ["integer", "null"], "description": "0 std, 1 taiko, 2 catch, 3 mania; null for the beatmap's own mode" }
              },
              "required": ["user", "beatmap_id", "mode"],
              "additionalProperties": false
            }
            """),
        Tool("get_user_activity",
            "A player's recent events: new #1-#1000 ranks on maps, lost #1s, medals, ranked or uploaded beatmaps, name changes, supporter.",
            """
            {
              "type": "object",
              "properties": {
                "user": { "type": "string", "description": "osu! username or numeric user id" },
                "limit": { "type": "integer", "description": "1-15" }
              },
              "required": ["user", "limit"],
              "additionalProperties": false
            }
            """),
        Tool("get_chat_ranking",
            "Top players of the current group chat by pp, from the bot's own data (like /chatstats). Only works in groups.",
            """
            {
              "type": "object",
              "properties": {
                "mode": { "type": "integer", "description": "0 std, 1 taiko, 2 catch, 3 mania" },
                "count": { "type": "integer", "description": "1-20" }
              },
              "required": ["mode", "count"],
              "additionalProperties": false
            }
            """),
        Tool("get_bot_user",
            "What the bot knows about a Telegram user. Without a username: the person writing to you, with their linked osu! account, default mode, last saved stats and the players tracked in this chat. With a @username of someone in this chat: only their linked osu! nickname and mode.",
            """
            {
              "type": "object",
              "properties": {
                "telegram_username": { "type": ["string", "null"], "description": "Telegram @username of a chat member; null for the person writing to you" }
              },
              "required": ["telegram_username"],
              "additionalProperties": false
            }
            """),
        Tool("get_bot_help",
            "The guide to SosuBot's commands: what each does, its parameters, aliases and examples. Use it to explain a command or to point to the right one.",
            """
            {
              "type": "object",
              "properties": {
                "command": { "type": ["string", "null"], "description": "One command like /calc or /render; null for the whole guide" }
              },
              "required": ["command"],
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

    public IReadOnlyList<ResponseTool> Tools { get; } = Definitions
        .Select(tool => (ResponseTool)ResponseTool.CreateFunctionTool(tool.Name, BinaryData.FromString(tool.Schema),
            strictModeEnabled: true, functionDescription: tool.Description))
        .ToArray();

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
                "get_ranking" => await GetRankingAsync(args, cancellationToken),
                "search_wiki" => await SearchWikiAsync(args, cancellationToken),
                "get_wiki_page" => await GetWikiPageAsync(args, cancellationToken),
                "get_news" => await GetNewsAsync(args, cancellationToken),
                "get_news_post" => await GetNewsPostAsync(args, cancellationToken),
                "get_changelog" => await GetChangelogAsync(args, cancellationToken),
                "get_spotlights" => await GetSpotlightsAsync(args, cancellationToken),
                "get_beatmapset" => await GetBeatmapsetAsync(args, cancellationToken),
                "get_beatmap_leaderboard" => await GetBeatmapLeaderboardAsync(args, cancellationToken),
                "get_user_beatmap_score" => await GetUserBeatmapScoreAsync(args, cancellationToken),
                "get_user_activity" => await GetUserActivityAsync(args, cancellationToken),
                "get_chat_ranking" => await GetChatRankingAsync(args, context, cancellationToken),
                "get_bot_user" => await GetBotUserAsync(args, context, cancellationToken),
                "get_bot_help" => GetBotHelp(args),
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

    private static AiToolDefinition Tool(string name, string description, string schema) =>
        new(name, description, schema);

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

    /// <summary>GET on osu! API v2; <paramref name="pathAndQuery" /> starts with "/". <c>null</c> on 404.</summary>
    private async Task<T?> ApiAsync<T>(string pathAndQuery, CancellationToken cancellationToken) where T : JsonNode =>
        await osuApi.MakeRequestAsync<T>(BanchoApiV2.ApiMainFunctionsBaseAddress.TrimEnd('/') + pathAndQuery,
            HttpMethod.Get, cancellationToken: cancellationToken);

    private static string? Str(JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static long? Long(JsonNode? node, string key) =>
        node?[key] is JsonValue value
            ? value.TryGetValue(out long number) ? number
            : value.TryGetValue(out double real) ? (long)real : null
            : null;

    private static double? Dbl(JsonNode? node, string key, int digits = 2) =>
        node?[key] is JsonValue value && value.TryGetValue(out double number) ? Math.Round(number, digits) : null;

    private static bool? Bool(JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    /// <summary>The date part of an ISO timestamp from the API.</summary>
    private static string? Date(JsonNode? node, string key) =>
        Str(node, key) is { Length: >= 10 } text ? text[..10] : null;

    private static IEnumerable<JsonNode> Items(JsonNode? node) =>
        node is JsonArray array ? array.OfType<JsonNode>() : [];

    private static string ReadString(JsonElement args, string property) =>
        args.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static int? ReadInt(JsonElement args, string property) =>
        args.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static int ReadInt(JsonElement args, string property, int min, int max, int fallback) =>
        Math.Clamp(ReadInt(args, property) ?? fallback, min, max);

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
