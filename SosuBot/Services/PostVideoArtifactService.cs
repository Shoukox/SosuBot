using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OsuApi;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;

namespace SosuBot.Services;

public sealed class PostVideoArtifactService(
    BanchoApiV2 osuApi,
    CachingHelper cachingHelper,
    VideoPreviewService videoPreviewService,
    ILogger<PostVideoArtifactService> logger)
{
    public async Task<PostVideoArtifacts> GenerateAsync(
        long scoreId,
        Score score,
        string outputDirectory,
        string skinName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(score);
        Directory.CreateDirectory(outputDirectory);

        int beatmapId = score.BeatmapId ?? score.Beatmap?.Id
            ?? throw new InvalidOperationException("The score does not contain a beatmap id.");
        BeatmapExtended beatmap = await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi, cancellationToken)
            ?? throw new InvalidOperationException($"Beatmap {beatmapId} was not found.");

        int beatmapsetId = beatmap.BeatmapsetId ?? score.Beatmapset?.Id
            ?? throw new InvalidOperationException("The score does not contain a beatmapset id.");
        BeatmapsetExtended beatmapset = await cachingHelper.GetOrCacheBeatmapset(beatmapsetId, osuApi)
            .WaitAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Beatmapset {beatmapsetId} was not found.");

        UserExtend? scoreUser = null;
        if (score.UserId is { } scoreUserId)
        {
            scoreUser = (await osuApi.Users.GetUser(
                scoreUserId.ToString(CultureInfo.InvariantCulture),
                new GetUserQueryParameters(),
                cancellationToken: cancellationToken))?.UserExtend;
        }

        string username = CleanOneLine(scoreUser?.Username ?? score.User?.Username ?? "unknown");
        string beatmapTitle = CleanOneLine(beatmapset.Title ?? "Unknown map");
        string difficultyName = CleanOneLine(beatmap.Version ?? "Unknown difficulty");
        string mapper = CleanOneLine(beatmapset.Creator ?? "unknown");
        string mapUrl = $"https://osu.ppy.sh/beatmaps/{beatmapId}";
        string scoreUrl = $"https://osu.ppy.sh/scores/{scoreId}";
        string mapperUrl = beatmapset.UserId is { } mapperId
            ? $"https://osu.ppy.sh/users/{mapperId}"
            : $"https://osu.ppy.sh/users/{Uri.EscapeDataString(mapper)}";
        string userUrl = scoreUser?.Id is { } userId
            ? $"https://osu.ppy.sh/users/{userId}"
            : score.UserId is { } profileScoreUserId
                ? $"https://osu.ppy.sh/users/{profileScoreUserId}"
                : "https://osu.ppy.sh/";

        GetBeatmapAttributesResponse? attributes = await osuApi.Beatmaps.GetBeatmapAttributes(
            beatmapId,
            new GetBeatmapAttributesRequest { Mods = score.Mods ?? [] },
            cancellationToken);
        double starRating = attributes?.DifficultyAttributes?.StarRating
                             ?? score.Beatmap?.DifficultyRating
                             ?? beatmap.DifficultyRating
                             ?? 0;

        string title = BuildTitle(score, beatmap, beatmapTitle, difficultyName, username, starRating);
        string description = BuildDescription(
            score,
            beatmap,
            beatmapset,
            mapUrl,
            mapperUrl,
            mapper,
            userUrl,
            scoreUrl,
            username,
            starRating,
            skinName);

        string titlePath = Path.Combine(outputDirectory, "title.txt");
        string descriptionPath = Path.Combine(outputDirectory, "description.txt");
        await File.WriteAllTextAsync(titlePath, title, cancellationToken);
        await File.WriteAllTextAsync(descriptionPath, description, cancellationToken);

        string? previewPath = null;
        try
        {
            ScorePreviewText previewText = new(
            [new ScorePreviewTextRun(title, "#FFFFFF", 0.25f)]);
            VideoPreviewGenerationResult preview = await videoPreviewService.GenerateAsync(
                scoreId,
                previewText,
                cancellationToken);
            if (preview.Image is not null)
            {
                previewPath = Path.Combine(outputDirectory, "preview.png");
                await File.WriteAllBytesAsync(previewPath, preview.Image, cancellationToken);
            }
            else
            {
                logger.LogWarning("Could not generate a preview for postvideo score {ScoreId}: {Failure}",
                    scoreId, preview.Failure);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not generate a preview for postvideo score {ScoreId}", scoreId);
        }

        return new PostVideoArtifacts(outputDirectory, titlePath, descriptionPath, previewPath, title, description);
    }

    private static string BuildTitle(
        Score score,
        BeatmapExtended beatmap,
        string beatmapTitle,
        string difficultyName,
        string username,
        double starRating)
    {
        string modePrefix = beatmap.Mode?.ToLowerInvariant() switch
        {
            "osu" or null => string.Empty,
            "taiko" => "[osu!taiko] ",
            "fruits" or "catch" => "[osu!catch] ",
            "mania" => "[osu!mania] ",
            _ => string.Empty
        };
        double accuracy = Math.Truncate((score.Accuracy ?? 0) * 10000) / 100;
        string result = score.IsPerfectCombo == true
            ? "FC"
            : $"{score.Statistics?.Miss ?? 0} misses";
        string pp = score.Pp is { } ppValue
            ? Math.Round(ppValue).ToString("0", CultureInfo.InvariantCulture)
            : "0";

        return $"{modePrefix}{starRating.ToString("0.##", CultureInfo.InvariantCulture)} ⭐ {username} | " +
               $"{beatmapTitle} [{difficultyName}] {accuracy.ToString("00.00", CultureInfo.InvariantCulture)}% " +
               $"{result} | {pp}pp";
    }

    private static string BuildDescription(
        Score score,
        BeatmapExtended beatmap,
        BeatmapsetExtended beatmapset,
        string mapUrl,
        string mapperUrl,
        string mapper,
        string userUrl,
        string scoreUrl,
        string username,
        double starRating,
        string skinName)
    {
        string mods = score.Mods is { Length: > 0 }
            ? string.Join("", score.Mods.Select(mod => mod.Acronym?.ToUpperInvariant()).OfType<string>())
            : "NM";
        double bpm = beatmap.BPM ?? 0;
        string playerTag = new(username.Where(char.IsLetterOrDigit).ToArray());
        if (playerTag.Length == 0)
            playerTag = "osu";

        var output = new StringBuilder();
        output.AppendLine("👇🇺🇿 uzOSU! community 🇺🇿👇");
        output.AppendLine("Telegram channel - https://t.me/uzbOsu");
        output.AppendLine("Telegram chat - https://t.me/UzOsuAL");
        output.AppendLine();
        output.AppendLine("//🎶Beatmap info🎶");
        output.AppendLine($"Map link: {mapUrl}");
        output.AppendLine($"Mapper: {mapperUrl} ({mapper})");
        output.AppendLine($"⭐{starRating.ToString("0.##", CultureInfo.InvariantCulture)} | " +
                          $"{bpm.ToString("0.##", CultureInfo.InvariantCulture)}bpm | " +
                          $"AR: {FormatValue(beatmap.AR)} | CS: {FormatValue(beatmap.CS)} | " +
                          $"OD: {FormatValue(beatmap.Accuracy)} | HP: {FormatValue(beatmap.Drain)}");
        output.AppendLine();
        output.AppendLine("//👤Player info👤");
        output.AppendLine($"Profile: {userUrl}");
        output.AppendLine($"Score: {scoreUrl}");
        output.AppendLine();
        output.AppendLine("//Useful links");
        output.AppendLine($"Skin used in replay: {CleanOneLine(skinName)}");
        output.AppendLine("osu! related telegram bot: @shiukkz2bot");
        output.AppendLine();
        output.AppendLine("osu! is a free to play online rhythm game, which you can use as a rhythm trainer online with lots of gameplay music! https://osu.ppy.sh/");
        output.AppendLine();
        output.AppendLine($"#osu #uzosu #{playerTag} #ru #en #english #russian #uzbekistan #uzbek #osugame #{mods.ToLowerInvariant()}");
        return output.ToString();
    }

    private static string FormatValue(double? value) =>
        value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—";

    private static string CleanOneLine(string value) =>
        value.ReplaceLineEndings(" ").Trim();
}

public sealed record PostVideoArtifacts(
    string DirectoryPath,
    string TitlePath,
    string DescriptionPath,
    string? PreviewPath,
    string Title,
    string Description);
