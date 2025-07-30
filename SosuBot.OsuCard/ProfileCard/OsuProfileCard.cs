using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.Graphics.ProfileCard
{
    public class OsuProfileCard
    {
        private Point _cardSize = new Point(1280, 720);
        private Point _avatarSize = new Point(65, 65);
        private int _avatarBorderRadius = 20; //px

        private Color _bgColor = Color.FromRgb(30, 32, 42);
        private Color _rankColor = Color.FromRgb(240, 219, 228);

        private OsuProfileCardInfo _profileInfo;
        private FontFamily _fontFamilyInter;

        private static readonly HttpClient httpClient = new HttpClient();

        public OsuProfileCard(OsuProfileCardInfo osuProfileCardInfo)
        {
            _profileInfo = osuProfileCardInfo;

            // Initialize fonts
            FontCollection _fonts = new FontCollection();
            _fontFamilyInter = _fonts.Add("fonts/Inter-Medium.ttf");
        }

        public void CreateCard()
        {
            using Image<Rgba32> image = new Image<Rgba32>(_cardSize.X, _cardSize.Y);
            image.Mutate(DrawProfileCard);
            image.SaveAsPng($"{_profileInfo.Username}.png");
        }

        private void DrawProfileCard(IImageProcessingContext context)
        {
            Font fontTitle = _fontFamilyInter.CreateFont(ProfileCardFontSize.Username);
            Font fontLabels = _fontFamilyInter.CreateFont(ProfileCardFontSize.Labels);
            Font fontValues = _fontFamilyInter.CreateFont(ProfileCardFontSize.Values);
            Font fontRank = _fontFamilyInter.CreateFont(ProfileCardFontSize.RankValue, FontStyle.Bold);

            context.Fill(_bgColor);

            // Draw osu! logo
            DrawProfileAvatarSection(context);

            // Draw username
            context.DrawText(_profileInfo.Username, fontTitle, Color.White, new PointF(130, 40));
            context.DrawText("User", fontValues, Color.Gray, new PointF(130, 80));

            // Stats
            context.Fill(Color.FromRgb(40, 42, 54), new RectangularPolygon(30, 120, 450, 80));
            context.DrawText($"Performance", fontLabels, Color.Gray, new PointF(40, 130));
            context.DrawText($"{_profileInfo.PP} pp", fontValues, Color.White, new PointF(40, 160));

            context.DrawText($"Accuracy", fontLabels, Color.Gray, new PointF(180, 130));
            context.DrawText($"{_profileInfo.Accuracy:F2}%", fontValues, Color.White, new PointF(180, 160));

            context.DrawText($"Play Count", fontLabels, Color.Gray, new PointF(340, 130));
            context.DrawText($"test", fontValues, Color.White, new PointF(340, 160));

            // Global Rank
            context.DrawText($"#test", fontRank, _rankColor, new PointF(180, 220));
        }

        private void DrawProfileAvatarSection(IImageProcessingContext context)
        {
            //rgb(70, 57, 63)
            context.Fill(Color.FromRgb(70, 57, 63), new RectangularPolygon(0, 0, _cardSize.X, 85));

            Stream avatarStream = httpClient.GetStreamAsync(_profileInfo.AvatarUrl).Result;
            Image avatar = Image.Load(avatarStream);
            avatar.Mutate(context =>
            {
                context.Resize(new ResizeOptions()
                {
                    Size = new Size(_avatarSize.X, _avatarSize.Y),
                    Mode = ResizeMode.Crop,
                    
                });
                IPathCollection corners = BuildCornersForRoundedImage(_avatarSize.X, _avatarSize.Y, _avatarBorderRadius);
                context.SetGraphicsOptions(new GraphicsOptions()
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.DestOut,
                });

                foreach (IPath path in corners)
                {
                    context = context.Fill(_bgColor, path);
                }
            });

            Point roundedAvatarLocation = new Point(50, 10);
            context.DrawImage(avatar, roundedAvatarLocation, 1);
        }

        private PathCollection BuildCornersForRoundedImage(int imageWidth, int imageHeight, float cornerRadius)
        {
            // First create a square
            var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

            // Then cut out of the square a circle so we are left with a corner
            IPath cornerTopLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

            // Corner is now a corner shape positions top left
            // let's make 3 more positioned correctly, we can do that by translating the original around the center of the image.
            float rightPos = imageWidth - cornerTopLeft.Bounds.Width + 1;
            float bottomPos = imageHeight - cornerTopLeft.Bounds.Height + 1;

            // Move it across the width of the image - the width of the shape
            IPath cornerTopRight = cornerTopLeft.RotateDegree(90).Translate(rightPos, 0);
            IPath cornerBottomLeft = cornerTopLeft.RotateDegree(-90).Translate(0, bottomPos);
            IPath cornerBottomRight = cornerTopLeft.RotateDegree(180).Translate(rightPos, bottomPos);

            return new PathCollection(cornerTopLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
        }
    }
}
