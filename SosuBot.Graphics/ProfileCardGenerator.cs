using System.Globalization;
using System.Reflection;
using SosuBot.Graphics.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.Graphics;

public sealed class ProfileCardGenerator
{
    public const int CardWidth = 400;
    public const int CardHeight = 600;

    private static readonly Assembly Assembly = typeof(ProfileCardGenerator).Assembly;
    private static readonly string[] ResourceNames = Assembly.GetManifestResourceNames();

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

        using Image<Rgba32> card = LoadImage($"Assets/OsuCard/Card/{GetCardName(data.Skills.Accuracy)}.png");
        using Image<Rgba32> modeIcon = LoadImage($"Assets/OsuCard/Icon/bancho_{GetModeSuffix(data.Mode)}.png");
        using Image<Rgba32> fullStar = LoadImage("Assets/OsuCard/Star/full_star.png");
        using Image<Rgba32> halfStar = LoadImage("Assets/OsuCard/Star/half_star.png");
        using Image<Rgba32> avatar = LoadAvatar(data.Avatar, data.Username);

        modeIcon.Mutate(context => context.Resize(80, 80));
        avatar.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(320, 320),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));
        fullStar.Mutate(context => context.Resize(32, 31));
        halfStar.Mutate(context => context.Resize(32, 31));

        Font usernameFont = FitFont(data.Username, 32, 17, 220);
        Font statsFont = _fontFamily.CreateFont(28);

        card.Mutate(context =>
        {
            context.DrawImage(modeIcon, new Point(20, 20), 1);
            context.DrawImage(avatar, new Point(40, 110), 1);
            DrawCenteredText(context, data.Username, usernameFont, 150, 45, 220);

            DrawStat(context, "Aim:", data.Skills.Aim, statsFont, 444);
            DrawStat(context, "Speed:", data.Skills.Speed, statsFont, 477);
            DrawStat(context, "Accuracy:", data.Skills.Accuracy, statsFont, 510);
            DrawStars(context, data.Skills.Stars, fullStar, halfStar);
        });

        MemoryStream output = new();
        card.Save(output, new PngEncoder());
        output.Position = 0;
        return output;
    }

    private static string GetCardName(double accuracySkill) => accuracySkill switch
    {
        >= 900 => "master_osu",
        >= 825 => "ultra_rare_osu",
        >= 700 => "super_rare_osu",
        >= 525 => "elite_osu",
        >= 300 => "rare_osu",
        _ => "common_osu"
    };

    private static string GetModeSuffix(OsuGameMode mode) => mode switch
    {
        OsuGameMode.Osu => "std",
        OsuGameMode.Taiko => "taiko",
        OsuGameMode.Catch => "ctb",
        OsuGameMode.Mania => "mania",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown osu! mode.")
    };

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

    private static void DrawStat(IImageProcessingContext context, string label, double value, Font font, float y)
    {
        DrawTextWithShadow(context, label, font, new PointF(42, y));

        string formattedValue = value.ToString("0", CultureInfo.InvariantCulture);
        float valueWidth = TextMeasurer.MeasureSize(formattedValue, new TextOptions(font)).Width;
        DrawTextWithShadow(context, formattedValue, font, new PointF(350 - valueWidth, y));
    }

    private static void DrawTextWithShadow(IImageProcessingContext context, string text, Font font, PointF position)
    {
        context.DrawText(text, font, Color.FromRgba(0, 0, 0, 170), new PointF(position.X + 2, position.Y + 2));
        context.DrawText(text, font, Color.White, position);
    }

    private static void DrawStars(IImageProcessingContext context, double stars, Image<Rgba32> fullStar,
        Image<Rgba32> halfStar)
    {
        int fullStars = Math.Clamp((int)Math.Floor(stars), 0, 20);
        bool hasHalfStar = fullStars < 20 && stars - fullStars >= 0.5;
        int count = fullStars + (hasHalfStar ? 1 : 0);
        if (count == 0)
            return;

        const int spacing = 2;
        int width = count * fullStar.Width + (count - 1) * spacing;
        float scale = Math.Min(1, 370f / width);
        int starWidth = Math.Max(1, (int)Math.Round(fullStar.Width * scale));
        int starHeight = Math.Max(1, (int)Math.Round(fullStar.Height * scale));
        int scaledWidth = count * starWidth + (count - 1) * spacing;
        int startX = (CardWidth - scaledWidth) / 2;
        int y = 552 + (31 - starHeight) / 2;

        for (int index = 0; index < count; index++)
        {
            Image<Rgba32> source = hasHalfStar && index == count - 1 ? halfStar : fullStar;
            if (source.Width == starWidth && source.Height == starHeight)
            {
                context.DrawImage(source, new Point(startX + index * (starWidth + spacing), y), 1);
                continue;
            }

            using Image<Rgba32> resized = source.Clone(image => image.Resize(starWidth, starHeight));
            context.DrawImage(resized, new Point(startX + index * (starWidth + spacing), y), 1);
        }
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

        Image<Rgba32> placeholder = new(320, 320, Color.ParseHex("#2a2238"));
        Font initialFont = _fontFamily.CreateFont(120);
        string initial = username[..1].ToUpperInvariant();
        placeholder.Mutate(context => DrawCenteredText(context, initial, initialFont, 0, 82, 320));
        return placeholder;
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
            throw new FileNotFoundException($"Embedded profile-card resource '{path}' was not found.", path);

        return Assembly.GetManifestResourceStream(resourceName)
               ?? throw new FileNotFoundException($"Embedded profile-card resource '{path}' could not be opened.",
                   path);
    }
}
