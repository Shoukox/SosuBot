using System.Globalization;
using System.Reflection;
using SosuBot.Graphics.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.Graphics;

public sealed class ProfileCardGenerator
{
    private const int RenderScale = 2;

    public const int CardWidth = 400 * RenderScale;
    public const int CardHeight = 600 * RenderScale;

    private const float CardEdgeBlurRadius = 18 * RenderScale;
    private const float AvatarCornerRadius = 16 * RenderScale;

    private static readonly Assembly Assembly = typeof(ProfileCardGenerator).Assembly;
    private static readonly string[] ResourceNames = Assembly.GetManifestResourceNames();
    private static readonly HashSet<(long PlayerId, OsuGameMode Mode)> LegendaryPlayers =
    [
        (50265, OsuGameMode.Osu),
        (2558286, OsuGameMode.Osu),
        (5339515, OsuGameMode.Osu),
        (4650315, OsuGameMode.Osu),
        (58710, OsuGameMode.Osu),
        (211278, OsuGameMode.Osu),
        (352328, OsuGameMode.Osu),
        (713266, OsuGameMode.Osu),
        (4787150, OsuGameMode.Osu),
        (7562902, OsuGameMode.Osu),
        (2774767, OsuGameMode.Taiko),
        (6170507, OsuGameMode.Taiko),
        (11199742, OsuGameMode.Taiko),
        (983349, OsuGameMode.Taiko),
        (214187, OsuGameMode.Catch),
        (533210, OsuGameMode.Catch),
        (3885626, OsuGameMode.Catch),
        (4447639, OsuGameMode.Catch),
        (140148, OsuGameMode.Mania),
        (259972, OsuGameMode.Mania)
    ];

    private static readonly IReadOnlyDictionary<(long PlayerId, OsuGameMode Mode), string> SpecialStarNames =
        new Dictionary<(long PlayerId, OsuGameMode Mode), string>
        {
            [(124493, OsuGameMode.Osu)] = "chocomint",
            [(7990747, OsuGameMode.Osu)] = "diamond",
            [(8926244, OsuGameMode.Osu)] = "shooting_star",
            [(2611813, OsuGameMode.Osu)] = "paw"
        };

    private readonly FontFamily _fontFamily;

    public ProfileCardGenerator()
    {
        FontCollection fonts = new();
        using Stream font = OpenResource("Fonts/Aller_Lt.ttf");
        _fontFamily = fonts.Add(font);
    }

    public MemoryStream Generate(ProfileCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Username);

        using Image<Rgba32> card = LoadCard(GetCardName(data.Skills.Accuracy, data.PlayerId, data.Mode));
        using Image<Rgba32> modeIcon = LoadImage($"Assets/OsuCard/Icon/bancho_{GetModeSuffix(data.Mode)}.png");
        string starName = GetStarName(data.PlayerId, data.Mode);
        using Image<Rgba32> fullStar = LoadImage($"Assets/OsuCard/Star/full_{starName}.png");
        using Image<Rgba32> halfStar = LoadImage($"Assets/OsuCard/Star/half_{starName}.png");
        using Image<Rgba32> avatar = LoadAvatar(data.Avatar, data.Username);

        modeIcon.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(80 * RenderScale, 80 * RenderScale),
            Sampler = KnownResamplers.Lanczos3
        }));
        avatar.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(320 * RenderScale, 320 * RenderScale),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
            Sampler = KnownResamplers.Lanczos3
        }));
        using Image<Rgba32> roundedAvatar = CreateRoundedAvatar(avatar);
        fullStar.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(32 * RenderScale, 31 * RenderScale),
            Sampler = KnownResamplers.Lanczos3
        }));
        halfStar.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(32 * RenderScale, 31 * RenderScale),
            Sampler = KnownResamplers.Lanczos3
        }));

        Font usernameFont = FitFont(data.Username, 32 * RenderScale, 17 * RenderScale, 220 * RenderScale);
        Font statsFont = _fontFamily.CreateFont(28 * RenderScale);
        Font starRatingFont = _fontFamily.CreateFont(14 * RenderScale);

        card.Mutate(context =>
        {
            context.DrawImage(modeIcon, new Point(20 * RenderScale, 20 * RenderScale), 1);
            context.DrawImage(roundedAvatar, new Point(40 * RenderScale, 110 * RenderScale), 1);
            DrawCenteredText(context, data.Username, usernameFont, 150 * RenderScale, 45 * RenderScale,
                220 * RenderScale);

            DrawStats(context, data.Skills.GetMetrics(data.Mode), statsFont);
            DrawStars(context, data.Skills.Stars, fullStar, halfStar, starRatingFont);
        });

        MemoryStream output = new();
        card.Save(output, new PngEncoder());
        output.Position = 0;
        return output;
    }

    private static string GetCardName(double accuracySkill, long? playerId, OsuGameMode mode)
    {
        if (playerId is { } id)
        {
            string playerCardName = id.ToString(CultureInfo.InvariantCulture);
            
            // -_HateP_-
            if (playerCardName == "15160962")
            {
                return playerCardName;
            }
            
            if (mode == OsuGameMode.Osu && ResourceExists($"Assets/OsuCard/Card/{playerCardName}.png"))
                return playerCardName;
            
            if (mode == OsuGameMode.Mania && ResourceExists($"Assets/OsuCard/Card/{playerCardName}_mania.png"))
                return playerCardName + "_mania";
            
            if (LegendaryPlayers.Contains((id, mode)))
                return "legendary_osu";
        }

        return accuracySkill switch
        {
            >= 1100 => "legendary_osu",
            >= 900 => "master_osu",
            >= 825 => "ultra_rare_osu",
            >= 700 => "super_rare_osu",
            >= 525 => "elite_osu",
            >= 300 => "rare_osu",
            _ => "common_osu"
        };
    }

    private static string GetStarName(long? playerId, OsuGameMode mode) =>
        playerId is { } id && SpecialStarNames.TryGetValue((id, mode), out string? starName)
            ? starName
            : "star";

    private static Image<Rgba32> LoadCard(string cardName)
    {
        Image<Rgba32> card = LoadImage($"Assets/OsuCard/Card/{cardName}.png");

        if (card.Width != CardWidth || card.Height != CardHeight)
        {
            card.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(CardWidth, CardHeight),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        Rectangle contentBounds = FindContentBounds(card);
        if (contentBounds is { X: 0, Y: 0, Width: CardWidth, Height: CardHeight })
            return card;

        // Keep the original card sharp and use a blurred, enlarged copy of its
        // own content to fill the transparent edge. This prevents Telegram's
        // white background from becoming a visible frame around the card.
        Image<Rgba32> blurredBackground = card.Clone(context => context
            .Crop(contentBounds)
            .Resize(CardWidth, CardHeight)
            .GaussianBlur(CardEdgeBlurRadius));
        MakeOpaque(blurredBackground);
        blurredBackground.Mutate(context => context.DrawImage(card, Point.Empty, 1));
        card.Dispose();

        return blurredBackground;
    }

    private static Rectangle FindContentBounds(Image<Rgba32> image)
    {
        int left = image.Width;
        int top = image.Height;
        int right = -1;
        int bottom = -1;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].A == 0)
                        continue;

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        });

        return right < 0
            ? new Rectangle(0, 0, image.Width, image.Height)
            : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    private static void MakeOpaque(Image<Rgba32> image)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    row[x].A = byte.MaxValue;
            }
        });
    }

    private static string GetModeSuffix(OsuGameMode mode) => mode switch
    {
        OsuGameMode.Osu => "std",
        OsuGameMode.Taiko => "taiko",
        OsuGameMode.Catch => "ctb",
        OsuGameMode.Mania => "mania",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown osu! mode.")
    };

    private static Image<Rgba32> CreateRoundedAvatar(Image<Rgba32> avatar)
    {
        Image<Rgba32> roundedAvatar = new(avatar.Width, avatar.Height, Color.Transparent);
        IPath roundedRectangle = CreateRoundedRectanglePath(avatar.Width, avatar.Height,
            Math.Min(AvatarCornerRadius, Math.Min(avatar.Width, avatar.Height) / 2f));

        roundedAvatar.Mutate(context =>
        {
            context.SetGraphicsOptions(new GraphicsOptions
            {
                Antialias = true,
                AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver
            });
            context.Clip(roundedRectangle, clippedContext => clippedContext.DrawImage(avatar, Point.Empty, 1));
        });

        return roundedAvatar;
    }

    private static IPath CreateRoundedRectanglePath(int width, int height, float radius)
    {
        float diameter = radius * 2;
        PathBuilder path = new();

        path.MoveTo(new PointF(radius, 0));
        path.LineTo(new PointF(width - radius, 0));
        path.AddArc(new RectangleF(width - diameter, 0, diameter, diameter), 0, -90, 90);
        path.LineTo(new PointF(width, height - radius));
        path.AddArc(new RectangleF(width - diameter, height - diameter, diameter, diameter), 0, 0, 90);
        path.LineTo(new PointF(radius, height));
        path.AddArc(new RectangleF(0, height - diameter, diameter, diameter), 0, 90, 90);
        path.LineTo(new PointF(0, radius));
        path.AddArc(new RectangleF(0, 0, diameter, diameter), 0, 180, 90);
        path.CloseFigure();

        return path.Build();
    }

    private Font FitFont(string text, float initialSize, float minimumSize, float maximumWidth)
    {
        for (float size = initialSize; size >= minimumSize; size--)
        {
            Font candidate = _fontFamily.CreateFont(size);
            if (TextMeasurer.MeasureSize(text, new TextOptions(candidate)).Width <= maximumWidth)
                return candidate;
        }

        return _fontFamily.CreateFont(minimumSize);
    }

    private static void DrawCenteredText(IImageProcessingContext context, string text, Font font, float left,
        float top, float width)
    {
        float measuredWidth = TextMeasurer.MeasureSize(text, new TextOptions(font)).Width;
        PointF position = new(left + Math.Max((width - measuredWidth) / 2, 0), top);
        DrawTextWithShadow(context, text, font, position);
    }

    private static void DrawStats(IImageProcessingContext context, IReadOnlyList<PlayerSkillMetric> metrics, Font font)
    {
        const float firstRowY = 444 * RenderScale;
        const float rowHeight = 33 * RenderScale;

        for (int index = 0; index < metrics.Count; index++)
        {
            PlayerSkillMetric metric = metrics[index];
            DrawStat(context, $"{metric.Label}:", metric.Value, font, firstRowY + index * rowHeight);
        }
    }

    private static void DrawStat(IImageProcessingContext context, string label, double value, Font font, float y)
    {
        DrawTextWithShadow(context, label, font, new PointF(42 * RenderScale, y));

        string formattedValue = value.ToString("0", CultureInfo.InvariantCulture);
        float valueWidth = TextMeasurer.MeasureSize(formattedValue, new TextOptions(font)).Width;
        DrawTextWithShadow(context, formattedValue, font, new PointF(350 * RenderScale - valueWidth, y));
    }

    private static void DrawTextWithShadow(IImageProcessingContext context, string text, Font font, PointF position)
    {
        context.DrawText(text, font, Color.FromRgba(0, 0, 0, 170),
            new PointF(position.X + 2 * RenderScale, position.Y + 2 * RenderScale));
        context.DrawText(text, font, Color.White, position);
    }

    private static void DrawStars(IImageProcessingContext context, double stars, Image<Rgba32> fullStar,
        Image<Rgba32> halfStar, Font starRatingFont)
    {
        int fullStars = Math.Clamp((int)Math.Floor(stars), 0, 20);
        bool hasHalfStar = fullStars < 20 && stars - fullStars >= 0.5;
        int count = fullStars + (hasHalfStar ? 1 : 0);
        if (count > 0)
        {
            const int spacing = 2 * RenderScale;
            int width = count * fullStar.Width + (count - 1) * spacing;
            float scale = Math.Min(1, 370f * RenderScale / width);
            int starWidth = Math.Max(1, (int)Math.Round(fullStar.Width * scale));
            int starHeight = Math.Max(1, (int)Math.Round(fullStar.Height * scale));
            int scaledWidth = count * starWidth + (count - 1) * spacing;
            int startX = (CardWidth - scaledWidth) / 2;
            int y = 552 * RenderScale + (31 * RenderScale - starHeight) / 2;

            for (int index = 0; index < count; index++)
            {
                Image<Rgba32> source = hasHalfStar && index == count - 1 ? halfStar : fullStar;
                if (source.Width == starWidth && source.Height == starHeight)
                {
                    context.DrawImage(source, new Point(startX + index * (starWidth + spacing), y), 1);
                    continue;
                }

                using Image<Rgba32> resized = source.Clone(image => image.Resize(new ResizeOptions
                {
                    Size = new Size(starWidth, starHeight),
                    Sampler = KnownResamplers.Lanczos3
                }));
                context.DrawImage(resized, new Point(startX + index * (starWidth + spacing), y), 1);
            }
        }

        double displayedStars = double.IsFinite(stars) ? Math.Max(0, stars) : 0;
        string starRating = displayedStars.ToString("0.00", CultureInfo.InvariantCulture) + "★";
        DrawCenteredText(context, starRating, starRatingFont, 0, 582 * RenderScale, CardWidth);
    }

    private Image<Rgba32> LoadAvatar(byte[]? avatarBytes, string username)
    {
        if (avatarBytes is { Length: > 0 })
        {
            try
            {
                return Image.Load<Rgba32>(avatarBytes);
            }
            catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
            {
                // A placeholder below keeps card generation available when osu! returns a broken avatar.
            }
        }

        Image<Rgba32> placeholder = new(320 * RenderScale, 320 * RenderScale, Color.ParseHex("#2a2238"));
        Font initialFont = _fontFamily.CreateFont(120 * RenderScale);
        string initial = username[..1].ToUpperInvariant();
        placeholder.Mutate(context =>
            DrawCenteredText(context, initial, initialFont, 0, 82 * RenderScale, 320 * RenderScale));
        return placeholder;
    }

    private static Image<Rgba32> LoadImage(string path)
    {
        using Stream resource = OpenResource(path);
        return Image.Load<Rgba32>(resource);
    }

    private static bool ResourceExists(string path) => ResourceNames.Any(name =>
        name.EndsWith(path.Replace('/', '.').Replace('\\', '.'), StringComparison.OrdinalIgnoreCase));

    private static Stream OpenResource(string path)
    {
        string suffix = path.Replace('/', '.').Replace('\\', '.');
        string? resourceName = ResourceNames.FirstOrDefault(name =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new FileNotFoundException($"Embedded profile-card resource '{path}' was not found.", path);

        return Assembly.GetManifestResourceStream(resourceName)
               ?? throw new FileNotFoundException($"Embedded profile-card resource '{path}' could not be opened.",
                   path);
    }
}
