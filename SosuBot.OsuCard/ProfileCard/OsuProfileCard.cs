using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.OsuCard.ProfileCard;

public class OsuProfileCard
{
    private static readonly HttpClient HttpClient = new();
    private readonly int _avatarBorderRadius = 20; //px
    private readonly Point _avatarSize = new(65, 65);

    private readonly Color _bgColor = Color.FromRgb(30, 32, 42);
    private readonly Point _cardSize = new(1280, 720);
    private readonly FontFamily _fontFamilyInter;

    private readonly OsuProfileCardInfo _profileInfo;
    private readonly Color _rankColor = Color.FromRgb(240, 219, 228);

    public OsuProfileCard(OsuProfileCardInfo osuProfileCardInfo)
    {
        _profileInfo = osuProfileCardInfo;

        // Initialize fonts
        var fonts = new FontCollection();
        _fontFamilyInter = fonts.Add("fonts/Inter-Medium.ttf");
    }

    public void CreateCard()
    {
        using var image = new Image<Rgba32>(_cardSize.X, _cardSize.Y);
        image.Mutate(DrawProfileCard);
        image.SaveAsPng($"{_profileInfo.Username}.png");
    }

    private void DrawProfileCard(IImageProcessingContext context)
    {
        var fontTitle = _fontFamilyInter.CreateFont(ProfileCardFontSize.Username);
        var fontLabels = _fontFamilyInter.CreateFont(ProfileCardFontSize.Labels);
        var fontValues = _fontFamilyInter.CreateFont(ProfileCardFontSize.Values);
        var fontRank = _fontFamilyInter.CreateFont(ProfileCardFontSize.RankValue, FontStyle.Bold);

        context.Fill(_bgColor);

        // Draw osu! logo
        DrawProfileAvatarSection(context);

        // Draw username
        context.DrawText(_profileInfo.Username, fontTitle, Color.White, new PointF(130, 40));
        context.DrawText("User", fontValues, Color.Gray, new PointF(130, 80));

        // Stats
        context.Fill(Color.FromRgb(40, 42, 54), new RectangularPolygon(30, 120, 450, 80));
        context.DrawText("Performance", fontLabels, Color.Gray, new PointF(40, 130));
        context.DrawText($"{_profileInfo.PP} pp", fontValues, Color.White, new PointF(40, 160));

        context.DrawText("Accuracy", fontLabels, Color.Gray, new PointF(180, 130));
        context.DrawText($"{_profileInfo.Accuracy:F2}%", fontValues, Color.White, new PointF(180, 160));

        context.DrawText("Play Count", fontLabels, Color.Gray, new PointF(340, 130));
        context.DrawText("test", fontValues, Color.White, new PointF(340, 160));

        // Global Rank
        context.DrawText("#test", fontRank, _rankColor, new PointF(180, 220));
    }

    private void DrawProfileAvatarSection(IImageProcessingContext context)
    {
        //rgb(70, 57, 63)
        context.Fill(Color.FromRgb(70, 57, 63), new RectangularPolygon(0, 0, _cardSize.X, 85));

        var avatarStream = HttpClient.GetStreamAsync(_profileInfo.AvatarUrl).Result;
        var avatar = Image.Load(avatarStream);
        avatar.Mutate(processingContext =>
        {
            processingContext.Resize(new ResizeOptions
            {
                Size = new Size(_avatarSize.X, _avatarSize.Y),
                Mode = ResizeMode.Crop
            });
            IPathCollection corners = BuildCornersForRoundedImage(_avatarSize.X, _avatarSize.Y, _avatarBorderRadius);
            processingContext.SetGraphicsOptions(new GraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
            });

            foreach (var path in corners) processingContext = processingContext.Fill(_bgColor, path);
        });

        var roundedAvatarLocation = new Point(50, 10);
        context.DrawImage(avatar, roundedAvatarLocation, 1);
    }

    private PathCollection BuildCornersForRoundedImage(int imageWidth, int imageHeight, float cornerRadius)
    {
        // First create a square
        var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

        // Then cut out of the square a circle so we are left with a corner
        var cornerTopLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

        // Corner is now a corner shape positions top left
        // let's make 3 more positioned correctly, we can do that by translating the original around the center of the image.
        var rightPos = imageWidth - cornerTopLeft.Bounds.Width + 1;
        var bottomPos = imageHeight - cornerTopLeft.Bounds.Height + 1;

        // Move it across the width of the image - the width of the shape
        var cornerTopRight = cornerTopLeft.RotateDegree(90).Translate(rightPos, 0);
        var cornerBottomLeft = cornerTopLeft.RotateDegree(-90).Translate(0, bottomPos);
        var cornerBottomRight = cornerTopLeft.RotateDegree(180).Translate(rightPos, bottomPos);

        return new PathCollection(cornerTopLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
    }
}