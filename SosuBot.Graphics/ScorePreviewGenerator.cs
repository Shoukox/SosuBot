using SosuBot.Graphics.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Reflection;

namespace SosuBot.Graphics;

public sealed class ScorePreviewGenerator
{
    public const int PreviewWidth = 1920;
    public const int PreviewHeight = 1080;

    private const int DefaultOffsetY = 10;
    private const int BottomTextMaximumWidth = 1720;

    private static readonly Assembly Assembly = typeof(ScorePreviewGenerator).Assembly;
    private static readonly string[] ResourceNames = Assembly.GetManifestResourceNames();

    private static readonly IReadOnlyDictionary<string, string> ModAssetNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AT"] = "selection-mod-autoPLAY@2x.png",
            ["CN"] = "selection-mod-cinema@2x.png",
            ["DT"] = "selection-mod-doubletime@2x.png",
            ["EZ"] = "selection-mod-easy@2x.png",
            ["FL"] = "selection-mod-flashlight@2x.png",
            ["HT"] = "selection-mod-halftime@2x.png",
            ["HR"] = "selection-mod-hardrock@2x.png",
            ["HD"] = "selection-mod-hidden@2x.png",
            ["NC"] = "selection-mod-nightcore@2x.png",
            ["NF"] = "selection-mod-nofail@2x.png",
            ["PF"] = "selection-mod-perfect@2x.png",
            ["RX"] = "selection-mod-relax@2x.png",
            ["AP"] = "selection-mod-relax2@2x.png",
            ["V2"] = "selection-mod-scorev2@2x.png",
            ["SV2"] = "selection-mod-scorev2@2x.png",
            ["SO"] = "selection-mod-spunout@2x.png",
            ["SD"] = "selection-mod-suddendeath@2x.png"
        };

    private readonly FontFamily _quicksandBold;
    private readonly FontFamily _quicksandSemibold;

    public ScorePreviewGenerator()
    {
        FontCollection fonts = new();
        using Stream boldFont = OpenResource("Fonts/Quicksand-Bold.ttf");
        using Stream semiboldFont = OpenResource("Fonts/Quicksand-SemiBold.ttf");
        _quicksandBold = fonts.Add(boldFont);
        _quicksandSemibold = fonts.Add(semiboldFont);
    }

    public MemoryStream Generate(ScorePreviewData data, ScorePreviewText bottomText)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(bottomText);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.BeatmapTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.DifficultyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Rank);
        if (bottomText.Runs.Count == 0 || string.IsNullOrWhiteSpace(bottomText.PlainText))
            throw new ArgumentException("Preview text must not be empty.", nameof(bottomText));

        using Image<Rgba32> mapBackground = LoadImageOrFallback(data.BackgroundImage,
            "Assets/ScorePreview/BGSotarks.png");
        using Image<Rgba32> avatar = LoadAvatar(data.AvatarImage, data.Username);
        using Image<Rgba32> countryFlag = LoadCountryFlag(data.CountryFlagImage, data.CountryCode);
        using Image<Rgba32> panelLineLeft = LoadImage("Assets/ScorePreview/panel_line_left.png");
        using Image<Rgba32> panelLineRight = LoadImage("Assets/ScorePreview/panel_line_right.png");
        using Image<Rgba32> fcPlaceholder = LoadImage("Assets/ScorePreview/fc_placeholder.png");
        using Image<Rgba32> ppPlaceholder = LoadImage("Assets/ScorePreview/pp_placeholder.png");
        using Image<Rgba32> nicknamePlaceholder = LoadImage("Assets/ScorePreview/nickname_placeholder.png");
        using Image<Rgba32> accuracyPlaceholder = LoadImage("Assets/ScorePreview/accuracy_placeholder.png");
        using Image<Rgba32> comboPlaceholder = LoadImage("Assets/ScorePreview/combo_placeholder.png");
        using Image<Rgba32> avatarPlaceholder = LoadImage("Assets/ScorePreview/avatar_placeholder.png");
        using Image<Rgba32> ranked = LoadImage("Assets/ScorePreview/ranked.png");
        List<Image<Rgba32>> mods = LoadMods(data.Mods);

        try
        {
            ResizeImages(mapBackground, avatar, countryFlag, panelLineLeft, panelLineRight, fcPlaceholder,
                ppPlaceholder, accuracyPlaceholder, comboPlaceholder, mods);

            string rankingLetter = data.Rank.ToUpperInvariant();
            bool isSilver = rankingLetter is "SH" or "XH";
            if (isSilver)
                rankingLetter = rankingLetter[..1];

            mapBackground.Mutate(context => DrawPreview(
                context,
                data,
                bottomText,
                rankingLetter,
                isSilver,
                avatar,
                countryFlag,
                panelLineLeft,
                panelLineRight,
                fcPlaceholder,
                ppPlaceholder,
                nicknamePlaceholder,
                accuracyPlaceholder,
                comboPlaceholder,
                avatarPlaceholder,
                ranked,
                mods));

            MemoryStream output = new();
            mapBackground.Save(output, new PngEncoder());
            output.Position = 0;
            return output;
        }
        finally
        {
            foreach (Image<Rgba32> mod in mods)
                mod.Dispose();
        }
    }

    private void DrawPreview(
        IImageProcessingContext context,
        ScorePreviewData data,
        ScorePreviewText bottomText,
        string rankingLetter,
        bool isSilver,
        Image<Rgba32> avatar,
        Image<Rgba32> countryFlag,
        Image<Rgba32> panelLineLeft,
        Image<Rgba32> panelLineRight,
        Image<Rgba32> fcPlaceholder,
        Image<Rgba32> ppPlaceholder,
        Image<Rgba32> nicknamePlaceholder,
        Image<Rgba32> accuracyPlaceholder,
        Image<Rgba32> comboPlaceholder,
        Image<Rgba32> avatarPlaceholder,
        Image<Rgba32> ranked,
        IReadOnlyList<Image<Rgba32>> mods)
    {
        context.Vignette(Color.Black);

        Font mapNameFont = _quicksandBold.CreateFont(140);
        FontRectangle mapNameSize = TextMeasurer.MeasureSize(data.BeatmapTitle, new TextOptions(mapNameFont));
        int mapNameX = Math.Max(0, (PreviewWidth - (int)mapNameSize.Width) / 2);
        bool mapNameOverflows = mapNameSize.Width > PreviewWidth - 100;
        ColorStop[] mapNameColors = mapNameOverflows
            ? [new(0, Color.White), new(0.9f, Color.White), new(1, Color.Transparent)]
            : [new(1, Color.White)];
        context.DrawGradientGlowingText(
            data.BeatmapTitle,
            mapNameFont,
            mapNameColors,
            mapNameColors,
            10,
            new Point(mapNameX, DefaultOffsetY),
            verticalGradient: !mapNameOverflows);

        Font mapDifficultyFont = _quicksandBold.CreateFont(64);
        FontRectangle mapDifficultySize = TextMeasurer.MeasureSize(data.DifficultyName,
            new TextOptions(mapDifficultyFont));
        int mapDifficultyX = Math.Max(0, (PreviewWidth - (int)mapDifficultySize.Width) / 2);
        int mapDifficultyY = DefaultOffsetY + (int)mapNameSize.Height + 10;
        context.DrawGlowingText(data.DifficultyName, mapDifficultyFont, Color.White, Color.White, 3,
            new Point(mapDifficultyX, mapDifficultyY));

        int panelY = (int)(PreviewHeight * 0.45);
        int panelHeight = PreviewHeight - panelY;
        context.FillPolygon(
            new DrawingOptions { GraphicsOptions = new GraphicsOptions { BlendPercentage = 0.7f } },
            Color.Black,
            [
                new PointF(0, panelY),
                new PointF(PreviewWidth, panelY),
                new PointF(PreviewWidth, PreviewHeight),
                new PointF(0, PreviewHeight)
            ]);
        context.GaussianBlur(10, new Rectangle(0, panelY, PreviewWidth, panelHeight));

        context.DrawImage(panelLineLeft, new Point(0, panelY - panelLineLeft.Height / 2), 1);
        context.DrawImage(panelLineRight,
            new Point(PreviewWidth - panelLineRight.Width, panelY - panelLineLeft.Height / 2), 1);

        const int placeholderMarginX = 160;
        int fcPlaceholderX = placeholderMarginX;
        int fcPlaceholderY = panelY - fcPlaceholder.Height + 2;
        context.DrawImage(fcPlaceholder, new Point(fcPlaceholderX, fcPlaceholderY), 1);

        int nicknamePlaceholderX = (PreviewWidth - nicknamePlaceholder.Width) / 2;
        int nicknamePlaceholderY = panelY - nicknamePlaceholder.Height + 2;
        context.DrawImage(nicknamePlaceholder, new Point(nicknamePlaceholderX, nicknamePlaceholderY), 1);

        int ppPlaceholderX = PreviewWidth - ppPlaceholder.Width - placeholderMarginX + 3;
        int ppPlaceholderY = panelY - ppPlaceholder.Height + 2;
        context.DrawImage(ppPlaceholder, new Point(ppPlaceholderX, ppPlaceholderY), 1);

        int avatarPlaceholderX = (PreviewWidth - avatarPlaceholder.Width) / 2;
        int avatarPlaceholderY = panelY;
        context.DrawImage(avatarPlaceholder, new Point(avatarPlaceholderX, avatarPlaceholderY), 1);

        int accuracyPlaceholderX = avatarPlaceholderX - accuracyPlaceholder.Width;
        int accuracyPlaceholderY = avatarPlaceholderY +
                                   (avatarPlaceholder.Height - accuracyPlaceholder.Height) / 2;
        context.DrawImage(accuracyPlaceholder, new Point(accuracyPlaceholderX, accuracyPlaceholderY), 1);

        int comboPlaceholderX = avatarPlaceholderX + avatarPlaceholder.Width;
        int comboPlaceholderY = avatarPlaceholderY + (avatarPlaceholder.Height - comboPlaceholder.Height) / 2;
        context.DrawImage(comboPlaceholder, new Point(comboPlaceholderX, comboPlaceholderY), 1);

        int avatarX = avatarPlaceholderX + 15;
        int avatarY = avatarPlaceholderY + 15;
        context.DrawImage(avatar, new Point(avatarX, avatarY), 1);

        int countryFlagX = avatarPlaceholderX + (avatarPlaceholder.Width - countryFlag.Width) / 2;
        int countryFlagY = avatarPlaceholderY + avatarPlaceholder.Height - countryFlag.Height / 2;
        context.DrawImage(countryFlag, new Point(countryFlagX, countryFlagY), 1);

        string countryRankText = data.CountryRank is { } countryRank ? $"#{countryRank}" : "#—";
        Font countryRankFont = _quicksandSemibold.CreateFont(64);
        FontRectangle countryRankSize = TextMeasurer.MeasureSize(countryRankText, new TextOptions(countryRankFont));
        int countryRankX = countryFlagX + (countryFlag.Width - (int)countryRankSize.Width) / 2;
        int countryRankY = countryFlagY + countryFlag.Height + 5;
        context.DrawGradientGlowingText(
            countryRankText,
            countryRankFont,
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            0,
            new Point(countryRankX, countryRankY));

        string fcText = data.IsFullCombo ? "FC" : $"{data.Misses}x";
        Font fcFont = _quicksandSemibold.CreateFont(110);
        FontRectangle fcSize = TextMeasurer.MeasureSize(fcText, new TextOptions(fcFont));
        int fcX = fcPlaceholderX + (fcPlaceholder.Width - (int)fcSize.Width) / 2 - 10;
        int fcY = fcPlaceholderY + (fcPlaceholder.Height - (int)fcSize.Height) / 2 - 10;
        ColorStop[] fcColors = data.IsFullCombo
            ? [new(0, Color.White), new(1, Color.ParseHex("7D7D7D"))]
            : [new(0, Color.ParseHex("DF1B1B")), new(1, Color.ParseHex("BA3030"))];
        context.DrawGradientGlowingText(fcText, fcFont, fcColors, fcColors, 0, new Point(fcX, fcY));

        Font nicknameFont = FitFont(data.Username, _quicksandSemibold, 64, 34,
            nicknamePlaceholder.Width - 40);
        FontRectangle nicknameSize = TextMeasurer.MeasureSize(data.Username, new TextOptions(nicknameFont));
        int nicknameX = nicknamePlaceholderX + (nicknamePlaceholder.Width - (int)nicknameSize.Width) / 2 - 10;
        int nicknameY = nicknamePlaceholderY + (nicknamePlaceholder.Height - (int)nicknameSize.Height) / 2;
        context.DrawGradientGlowingText(
            data.Username,
            nicknameFont,
            [new(0, Color.White), new(1, Color.ParseHex("999999"))],
            [new(0, Color.White), new(1, Color.ParseHex("999999"))],
            0,
            new Point(nicknameX, nicknameY));

        string ppText = data.PerformancePoints is { } pp && double.IsFinite(pp)
            ? $"{Math.Round(pp):0}pp"
            : "—pp";
        Font ppFont = FitFont(ppText, _quicksandSemibold, 100, 54, ppPlaceholder.Width - 35);
        FontRectangle ppSize = TextMeasurer.MeasureSize(ppText, new TextOptions(ppFont));
        int ppX = ppPlaceholderX + (ppPlaceholder.Width - (int)ppSize.Width) / 2 - 10;
        int ppY = ppPlaceholderY + (ppPlaceholder.Height - (int)ppSize.Height) / 2;
        context.DrawGradientGlowingText(
            ppText,
            ppFont,
            [new(0, Color.White), new(1, Color.ParseHex("999999"))],
            [new(0, Color.White), new(1, Color.ParseHex("999999"))],
            0,
            new Point(ppX, ppY));

        string accuracyText = $"{Math.Clamp(data.AccuracyPercent, 0, 100):00.00}%";
        Font accuracyFont = _quicksandSemibold.CreateFont(64);
        FontRectangle accuracySize = TextMeasurer.MeasureSize(accuracyText, new TextOptions(accuracyFont));
        int accuracyX = accuracyPlaceholderX + (accuracyPlaceholder.Width - (int)accuracySize.Width) / 2;
        int accuracyY = accuracyPlaceholderY + (accuracyPlaceholder.Height - (int)accuracySize.Height) / 2 - 5;
        context.DrawGradientGlowingText(
            accuracyText,
            accuracyFont,
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            0,
            new Point(accuracyX, accuracyY));

        string comboText = $"{Math.Max(data.Combo, 0)}x";
        Font comboFont = _quicksandSemibold.CreateFont(64);
        FontRectangle comboSize = TextMeasurer.MeasureSize(comboText, new TextOptions(comboFont));
        int comboX = comboPlaceholderX + (comboPlaceholder.Width - (int)comboSize.Width) / 2 - 15;
        context.DrawGradientGlowingText(
            comboText,
            comboFont,
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            0,
            new Point(comboX, accuracyY));

        DrawRank(context, rankingLetter, isSilver, panelY, panelHeight, fcPlaceholderX, fcPlaceholder.Width);
        DrawMods(context, mods, accuracyPlaceholderX, accuracyPlaceholder.Width,
            accuracyPlaceholderY + accuracyPlaceholder.Height);

        int rankedX = ppPlaceholderX;
        int rankedY = comboPlaceholderY + 20;
        context.DrawImage(ranked, new Point(rankedX, rankedY), 1);

        string starRatingText = Math.Max(data.StarRating, 0).ToString("0.00", CultureInfo.InvariantCulture);
        Font starRatingFont = _quicksandSemibold.CreateFont(96);
        FontRectangle starRatingSize = TextMeasurer.MeasureSize(starRatingText, new TextOptions(starRatingFont));
        int starRatingX = rankedX + ranked.Width + 30;
        int starRatingY = rankedY + (ranked.Height - (int)starRatingSize.Height) / 2 - 5;
        context.DrawGradientGlowingText(
            starRatingText,
            starRatingFont,
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            [new(0, Color.White), new(1, Color.ParseHex("C1C1C1"))],
            0,
            new Point(starRatingX, starRatingY));

        string bpmText = $"BPM\n{Math.Max(data.Bpm, 0):0.##}";
        Font bpmFont = _quicksandSemibold.CreateFont(96);
        FontRectangle bpmSize = TextMeasurer.MeasureSize(bpmText, new TextOptions(bpmFont));
        int bpmX = ppPlaceholderX + (ppPlaceholder.Width - (int)bpmSize.Width) / 2 - 10;
        int bpmY = avatarY + avatar.Height - 20;
        context.DrawGradientGlowingText(
            bpmText,
            bpmFont,
            [new(1, Color.ParseHex("D1D1D1"))],
            [new(1, Color.ParseHex("D1D1D1"))],
            0,
            new Point(bpmX, bpmY),
            textAlignment: TextAlignment.Center);

        int bottomTextY = countryRankY + (int)countryRankSize.Height + 60;
        DrawBottomText(context, bottomText, bottomTextY);

        using Image<Rgba32> easterEgg = countryFlag.Clone(image =>
            image.Resize(countryFlag.Width * 3, countryFlag.Height * 3));
        context.DrawImage(easterEgg, new Point(PreviewWidth - easterEgg.Width, 0), 0.03f);
    }

    private void DrawRank(IImageProcessingContext context, string rank, bool isSilver, int panelY,
        int panelHeight, int placeholderX, int placeholderWidth)
    {
        Font rankFont = _quicksandSemibold.CreateFont(450);
        FontRectangle rankSize = TextMeasurer.MeasureSize(rank, new TextOptions(rankFont));
        int rankX = placeholderX + (placeholderWidth - (int)rankSize.Width) / 2 - 25;
        int rankY = panelY + (panelHeight - (int)rankSize.Height) / 2 - 80;
        ColorStop[] gold =
        [
            new(0, Color.ParseHex("FEFE3B")),
            new(1, Color.ParseHex("C7C731")),
            new(1, Color.ParseHex("787824"))
        ];
        ColorStop[] silver = [new(0, Color.ParseHex("DEDEDE")), new(1, Color.ParseHex("6E6E6E"))];
        ColorStop[] green = [new(0, Color.ParseHex("17C200")), new(1, Color.ParseHex("0B5C00"))];
        ColorStop[] rankColors = rank switch
        {
            "X" or "S" => isSilver ? silver : gold,
            "A" => green,
            _ => gold
        };

        context.DrawGradientGlowingText(
            rank,
            rankFont,
            rankColors,
            rankColors,
            40,
            new Point(rankX, rankY),
            gradientOffset: new Point(0, 80),
            softenGlow: true);
    }

    private static void DrawMods(IImageProcessingContext context, IReadOnlyList<Image<Rgba32>> mods,
        int accuracyPlaceholderX, int accuracyPlaceholderWidth, int modsY)
    {
        const int spacing = 60;
        int modsX = accuracyPlaceholderX + accuracyPlaceholderWidth -
                    (mods.Count > 0 ? (mods.Count - 1) * spacing + mods[0].Width : 0);
        foreach (Image<Rgba32> mod in mods.Reverse())
        {
            context.DrawImage(mod, new Point(modsX, modsY), 1);
            modsX += spacing;
        }
    }

    private void DrawBottomText(IImageProcessingContext context, ScorePreviewText text, int y)
    {
        Font font = FitFont(text.PlainText, _quicksandBold, 84, 30, BottomTextMaximumWidth);
        float totalWidth = text.Runs.Sum(run =>
            TextMeasurer.MeasureSize(run.Text, new TextOptions(font)).Width);
        float x = Math.Max((PreviewWidth - totalWidth) / 2, 0);

        foreach (ScorePreviewTextRun run in text.Runs)
        {
            Color color = Color.ParseHex(run.ColorHex);
            context.DrawGlowingText(run.Text, font, color, color, run.GlowLevel,
                new Point((int)Math.Round(x), y));
            x += TextMeasurer.MeasureSize(run.Text, new TextOptions(font)).Width;
        }
    }

    private static Font FitFont(string text, FontFamily family, float initialSize, float minimumSize,
        float maximumWidth)
    {
        for (float size = initialSize; size >= minimumSize; size--)
        {
            Font candidate = family.CreateFont(size);
            if (TextMeasurer.MeasureSize(text, new TextOptions(candidate)).Width <= maximumWidth)
                return candidate;
        }

        return family.CreateFont(minimumSize);
    }

    private static void ResizeImages(
        Image<Rgba32> background,
        Image<Rgba32> avatar,
        Image<Rgba32> countryFlag,
        Image<Rgba32> panelLineLeft,
        Image<Rgba32> panelLineRight,
        Image<Rgba32> fcPlaceholder,
        Image<Rgba32> ppPlaceholder,
        Image<Rgba32> accuracyPlaceholder,
        Image<Rgba32> comboPlaceholder,
        IEnumerable<Image<Rgba32>> mods)
    {
        background.Mutate(image => image.Resize(new ResizeOptions
        {
            Size = new Size(PreviewWidth, PreviewHeight),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));
        panelLineLeft.Mutate(image => image.Resize(PreviewWidth / 2 + 2, panelLineLeft.Height));
        panelLineRight.Mutate(image => image.Resize(PreviewWidth / 2, panelLineRight.Height));
        fcPlaceholder.Mutate(image => image.Resize(335, 132));
        ppPlaceholder.Mutate(image => image.Resize(335, 132));
        countryFlag.Mutate(image => image.Resize(103, 75));
        comboPlaceholder.Mutate(image => image.Resize(accuracyPlaceholder.Width, accuracyPlaceholder.Height));
        avatar.Mutate(image => image.Resize(new ResizeOptions
        {
            Size = new Size(263, 263),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));

        foreach (Image<Rgba32> mod in mods)
            mod.Mutate(image => image.Resize(100, 100));
    }

    private List<Image<Rgba32>> LoadMods(IEnumerable<string> acronyms)
    {
        List<Image<Rgba32>> result = [];
        HashSet<string> loadedAssets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string acronym in acronyms)
        {
            if (!ModAssetNames.TryGetValue(acronym, out string? assetName) || !loadedAssets.Add(assetName))
                continue;

            result.Add(LoadImage($"Assets/ScorePreview/mods/{assetName}"));
        }

        return result;
    }

    private Image<Rgba32> LoadAvatar(byte[]? bytes, string username)
    {
        if (TryLoadExternalImage(bytes, out Image<Rgba32>? avatar))
            return avatar!;

        Image<Rgba32> placeholder = new(263, 263, Color.ParseHex("202027"));
        Font initialFont = _quicksandBold.CreateFont(120);
        string initial = username[..1].ToUpperInvariant();
        FontRectangle initialSize = TextMeasurer.MeasureSize(initial, new TextOptions(initialFont));
        placeholder.Mutate(context => context.DrawText(initial, initialFont, Color.White,
            new PointF((placeholder.Width - initialSize.Width) / 2, (placeholder.Height - initialSize.Height) / 2)));
        return placeholder;
    }

    private Image<Rgba32> LoadCountryFlag(byte[]? bytes, string? countryCode)
    {
        if (TryLoadExternalImage(bytes, out Image<Rgba32>? flag))
            return flag!;

        if (string.Equals(countryCode, "UZ", StringComparison.OrdinalIgnoreCase))
            return LoadImage("Assets/ScorePreview/uz.png");

        Image<Rgba32> placeholder = new(103, 75, Color.ParseHex("292933"));
        string label = string.IsNullOrWhiteSpace(countryCode) ? "??" : countryCode.ToUpperInvariant();
        Font font = _quicksandSemibold.CreateFont(34);
        FontRectangle size = TextMeasurer.MeasureSize(label, new TextOptions(font));
        placeholder.Mutate(context => context.DrawText(label, font, Color.White,
            new PointF((placeholder.Width - size.Width) / 2, (placeholder.Height - size.Height) / 2)));
        return placeholder;
    }

    private static Image<Rgba32> LoadImageOrFallback(byte[]? bytes, string fallbackPath) =>
        TryLoadExternalImage(bytes, out Image<Rgba32>? image) ? image! : LoadImage(fallbackPath);

    private static bool TryLoadExternalImage(byte[]? bytes, out Image<Rgba32>? image)
    {
        image = null;
        if (bytes is not { Length: > 0 })
            return false;

        try
        {
            image = Image.Load<Rgba32>(bytes);
            return true;
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            return false;
        }
    }

    private static Image<Rgba32> LoadImage(string path)
    {
        using Stream resource = OpenResource(path);
        return Image.Load<Rgba32>(resource);
    }

    private static Stream OpenResource(string path)
    {
        string suffix = path.Replace('/', '.').Replace('\\', '.');
        string? resourceName = ResourceNames.FirstOrDefault(name =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new FileNotFoundException($"Embedded score-preview resource '{path}' was not found.", path);

        return Assembly.GetManifestResourceStream(resourceName)
               ?? throw new FileNotFoundException($"Embedded score-preview resource '{path}' could not be opened.",
                   path);
    }
}
