using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SosuBot.Database.Models;
using SosuBot.Extensions;

namespace SosuBot.Services.Ai;

// Rankings and the osu! community: wiki, news, changelog, spotlights.
public sealed partial class AiToolbox
{
    private const int MaxWikiResults = 5;

    private async Task<object> GetRankingAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string type = ReadString(args, "type") is "score" or "country" ? ReadString(args, "type") : "performance";
        var mode = (Playmode)ReadInt(args, "mode", 0, 3, 0);
        string country = ReadString(args, "country_code").ToUpperInvariant();
        bool byCountry = type != "country" && CountryCode().IsMatch(country);
        int count = ReadInt(args, "count", 1, 50, 10);

        JsonObject? response = await ApiAsync<JsonObject>(
            $"/rankings/{mode.ToRuleset()}/{type}{(byCountry ? $"?country={country}" : "")}", cancellationToken);
        JsonNode[] entries = Items(response?["ranking"]).Take(count).ToArray();
        if (entries.Length == 0)
            return new { error = byCountry ? $"no ranking for country {country}" : "the ranking is empty" };

        if (type == "country")
        {
            return new
            {
                Type = "countries by total pp",
                Mode = mode.ToGamemode(),
                Countries = entries.Select((entry, index) => new
                {
                    Place = index + 1,
                    Country = Str(entry["country"], "name") ?? Str(entry, "code"),
                    Code = Str(entry, "code"),
                    ActivePlayers = Long(entry, "active_users"),
                    Pp = Long(entry, "performance"),
                    RankedScore = Long(entry, "ranked_score"),
                    PlayCount = Long(entry, "play_count")
                }).ToArray()
            };
        }

        return new
        {
            Type = type == "score" ? "players by ranked score" : "players by pp",
            Scope = byCountry ? country : "world",
            Mode = mode.ToGamemode(),
            Players = entries.Select((entry, index) => new
            {
                Place = index + 1,
                Username = Str(entry["user"], "username"),
                Country = byCountry ? null : Str(entry["user"], "country_code"),
                GlobalRank = Long(entry, "global_rank"),
                Pp = Dbl(entry, "pp", 0),
                RankedScore = type == "score" ? Long(entry, "ranked_score") : null,
                Accuracy = Dbl(entry, "hit_accuracy"),
                PlayCount = Long(entry, "play_count"),
                Link = Long(entry["user"], "id") is { } id ? $"https://osu.ppy.sh/users/{id}" : null
            }).ToArray()
        };
    }

    private async Task<object> SearchWikiAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string query = ReadString(args, "query");
        if (query.Length == 0)
            return new { error = "empty query" };

        string locale = WikiLocale(ReadString(args, "locale"));
        JsonObject? response = await ApiAsync<JsonObject>(
            $"/search?mode=wiki_page&query={Uri.EscapeDataString(query)}&locale={locale}", cancellationToken);
        var pages = Items(response?["wiki_page"]?["data"])
            .Take(MaxWikiResults)
            .Select(page => new
            {
                Title = Str(page, "title"),
                Subtitle = Str(page, "subtitle"),
                Path = Str(page, "path"),
                Locale = Str(page, "locale"),
                Snippet = Str(page, "markdown") is { } markdown ? Snippet(markdown) : null
            })
            .Where(page => page.Path is not null)
            .ToArray();

        return pages.Length == 0
            ? new { error = "nothing found in the wiki, try other or English words" }
            : new { Pages = pages };
    }

    private async Task<object> GetWikiPageAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string path = WikiPath(ReadString(args, "path"));
        if (path.Length == 0)
            return new { error = "empty path" };

        string locale = WikiLocale(ReadString(args, "locale"));
        JsonObject? page = await ApiAsync<JsonObject>($"/wiki/{locale}/{EscapePath(path)}", cancellationToken);
        if (page?["markdown"] is null && locale != "en")
        {
            locale = "en";
            page = await ApiAsync<JsonObject>($"/wiki/en/{EscapePath(path)}", cancellationToken);
        }

        if (Str(page, "markdown") is not { } markdown)
            return new { error = "no such wiki page, find the path with search_wiki" };

        string text = AiText.CleanWikiMarkdown(markdown);
        IReadOnlyList<AiText.Section> sections = AiText.SplitSections(text);
        string[] outline = sections
            .Where(section => section.Level is 2 or 3)
            .Select(section => (section.Level == 3 ? "  " : "") + section.Title)
            .ToArray();
        string link = $"https://osu.ppy.sh/wiki/{Str(page, "locale") ?? locale}/{Str(page, "path") ?? path}";

        string? sectionName = ReadString(args, "section") is { Length: > 0 } name ? name : null;
        if (sectionName is not null)
        {
            return AiText.FindSection(sections, sectionName) is { } sectionText
                ? new { Title = Str(page, "title"), Section = sectionName, Content = AiText.Truncate(sectionText), Link = link }
                : new { error = "no such section", Outline = outline };
        }

        bool whole = text.Length <= AiText.MaxLongText;
        return new
        {
            Title = Str(page, "title"),
            Subtitle = Str(page, "subtitle"),
            Locale = Str(page, "locale") ?? locale,
            Outline = whole ? null : outline,
            Content = whole ? text : AiText.Truncate(text, AiText.MaxLongText / 2),
            Note = whole ? null : "the page is long: ask for a section from the outline to read more",
            Link = link
        };
    }

    private async Task<object> GetNewsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int limit = ReadInt(args, "limit", 1, 8, 5);
        JsonObject? response = await ApiAsync<JsonObject>($"/news?limit={limit}", cancellationToken);
        var posts = Items(response?["news_posts"])
            .Take(limit)
            .Select(post => new
            {
                Title = Str(post, "title"),
                Date = Date(post, "published_at"),
                Author = Str(post, "author"),
                Preview = Str(post, "preview") is { } preview ? AiText.Truncate(AiText.HtmlToText(preview), 300) : null,
                Slug = Str(post, "slug"),
                Link = Str(post, "slug") is { } slug ? $"https://osu.ppy.sh/home/news/{slug}" : null
            })
            .ToArray();
        return posts.Length == 0 ? new { error = "no news" } : new { Posts = posts };
    }

    private async Task<object> GetNewsPostAsync(JsonElement args, CancellationToken cancellationToken)
    {
        // Accept a pasted link as well as a bare slug.
        string slug = ReadString(args, "slug").TrimEnd('/');
        slug = slug[(slug.LastIndexOf('/') + 1)..];
        if (!NewsSlug().IsMatch(slug))
            return new { error = "not a news slug" };

        JsonObject? post = await ApiAsync<JsonObject>($"/news/{slug}", cancellationToken);
        if (post is null)
            return new { error = "no such news post" };

        return new
        {
            Title = Str(post, "title"),
            Date = Date(post, "published_at"),
            Author = Str(post, "author"),
            Content = Str(post, "content") is { } content ? AiText.Truncate(AiText.HtmlToText(content)) : null,
            Link = $"https://osu.ppy.sh/home/news/{slug}"
        };
    }

    private async Task<object> GetChangelogAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string stream = ReadString(args, "stream") switch
        {
            "stable" => "stable40",
            "cuttingedge" => "cuttingedge",
            "web" => "web",
            _ => "lazer"
        };
        int limit = ReadInt(args, "limit", 1, 5, 2);
        JsonObject? response = await ApiAsync<JsonObject>($"/changelog?stream={stream}", cancellationToken);
        var builds = Items(response?["builds"])
            .Take(limit)
            .Select(build => new
            {
                Version = Str(build, "display_version"),
                Date = Date(build, "created_at"),
                Changes = Items(build["changelog_entries"])
                    .Take(15)
                    .Select(entry => new
                    {
                        Title = Str(entry, "title"),
                        Category = Str(entry, "category"),
                        Type = Str(entry, "type"),
                        Major = Bool(entry, "major") == true ? true : (bool?)null
                    })
                    .ToArray(),
                Link = Str(build, "display_version") is { } version
                    ? $"https://osu.ppy.sh/home/changelog/{stream}/{Uri.EscapeDataString(version)}"
                    : null
            })
            .ToArray();
        return builds.Length == 0 ? new { error = "no releases found" } : new { Stream = stream, Releases = builds };
    }

    private async Task<object> GetSpotlightsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var mode = (Playmode)ReadInt(args, "mode", 0, 3, 0);
        if (ReadInt(args, "spotlight_id") is not { } spotlightId)
        {
            JsonObject? list = await ApiAsync<JsonObject>("/spotlights", cancellationToken);
            var spotlights = Items(list?["spotlights"])
                .OrderByDescending(spotlight => Str(spotlight, "start_date"))
                .Take(10)
                .Select(spotlight => new
                {
                    Id = Long(spotlight, "id"),
                    Name = Str(spotlight, "name"),
                    Type = Str(spotlight, "type"),
                    From = Date(spotlight, "start_date"),
                    To = Date(spotlight, "end_date"),
                    Participants = Long(spotlight, "participant_count")
                })
                .ToArray();
            return spotlights.Length == 0 ? new { error = "no spotlights" } : new { Spotlights = spotlights };
        }

        JsonObject? chart = await ApiAsync<JsonObject>(
            $"/rankings/{mode.ToRuleset()}/charts?spotlight={spotlightId}", cancellationToken);
        if (chart is null)
            return new { error = "no such spotlight" };

        JsonNode[] beatmapsets = Items(chart["beatmapsets"]).ToArray();
        return new
        {
            Name = Str(chart["spotlight"], "name"),
            From = Date(chart["spotlight"], "start_date"),
            To = Date(chart["spotlight"], "end_date"),
            Mode = mode.ToGamemode(),
            MapCount = beatmapsets.Length,
            Maps = beatmapsets.Take(10).Select(set => $"{Str(set, "artist")} - {Str(set, "title")}").ToArray(),
            Players = Items(chart["ranking"]).Take(20).Select((entry, index) => new
            {
                Place = index + 1,
                Username = Str(entry["user"], "username"),
                Country = Str(entry["user"], "country_code"),
                RankedScore = Long(entry, "ranked_score"),
                Accuracy = Dbl(entry, "hit_accuracy"),
                PlayCount = Long(entry, "play_count")
            }).ToArray()
        };
    }

    /// <summary>A wiki locale the API knows; Uzbek and anything unknown read the English wiki.</summary>
    private static string WikiLocale(string locale)
    {
        locale = locale.Trim().ToLowerInvariant().Replace('_', '-');
        return locale.Length > 0 && locale != "uz" && WikiLocalePattern().IsMatch(locale) ? locale : "en";
    }

    /// <summary>The page path from a bare path or any wiki link, with spaces as underscores.</summary>
    public static string WikiPath(string path)
    {
        path = path.Trim().Replace(' ', '_');
        int wiki = path.IndexOf("/wiki/", StringComparison.OrdinalIgnoreCase);
        if (wiki >= 0)
            path = path[(wiki + "/wiki/".Length)..];
        path = path.Split('#', '?')[0].Trim('/');
        return LeadingLocale().Replace(path, "");
    }

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private static string Snippet(string markdown)
    {
        string text = AiText.CleanWikiMarkdown(markdown);
        string body = string.Join(' ', text.Split('\n').Where(line => line.Length > 0 && !line.StartsWith('#')));
        return AiText.Truncate(body, 200);
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();

    [GeneratedRegex("^[a-z]{2}(-[a-z]{2})?$")]
    private static partial Regex WikiLocalePattern();

    [GeneratedRegex("^[a-z]{2}(-[a-z]{2})?/")]
    private static partial Regex LeadingLocale();

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,200}$")]
    private static partial Regex NewsSlug();
}
