using PandoraBot.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PandoraBot.Services
{
    public static class CharacterCardService
    {
        private const int Width = 960;
        private const int Height = 640;

        public static MemoryStream Render(Hunter hunter, string ownerName)
        {
            var image = new Image<Rgba32>(Width, Height);
            using var ornament = LoadOrnamentBackground();
            var hpRatio = hunter.MaxHp > 0 ? Math.Clamp((double)hunter.CurrentHp / hunter.MaxHp, 0, 1) : 0;

            image.Mutate(ctx =>
            {
                DrawBackground(ctx, ornament);
                DrawFrame(ctx);
                DrawHeader(ctx, hunter, ownerName);
                DrawHpPanel(ctx, hunter, hpRatio);
                DrawStats(ctx, hunter);
                DrawFooter(ctx);
            });

            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            image.Dispose();
            stream.Position = 0;
            return stream;
        }

        private static void DrawBackground(IImageProcessingContext ctx, Image<Rgba32>? ornament)
        {
            ctx.Fill(Hex("080A11"));

            if (ornament != null)
            {
                ctx.DrawImage(ornament, new Point(0, 0), 0.28f);
                ctx.Fill(HexAlpha("050914", 112));
            }

            DrawLine(ctx, 76, 0, 226, Height, Hex("132040"), 2);
            DrawLine(ctx, 750, 0, 936, Height, Hex("181B2F"), 2);
            DrawLine(ctx, 0, 488, Width, 410, Hex("14223D"), 2);
            DrawLine(ctx, 16, 112, Width - 20, 84, Hex("101A34"), 1);
            DrawHudGrid(ctx);

            ctx.Fill(HexAlpha("0C101B", 238), new RectangularPolygon(34, 30, 892, 580));
            ctx.Fill(HexAlpha("151A2A", 242), new RectangularPolygon(48, 44, 864, 552));
            ctx.Draw(Hex("6CE0FF"), 2, new RectangularPolygon(48, 44, 864, 552));
            ctx.Draw(Hex("2F80ED"), 1, new RectangularPolygon(60, 56, 840, 528));
        }

        private static void DrawFrame(IImageProcessingContext ctx)
        {
            ctx.Fill(Hex("6CE0FF"), new RectangularPolygon(48, 44, 6, 70));
            ctx.Fill(Hex("6CE0FF"), new RectangularPolygon(48, 44, 170, 4));
            ctx.Fill(Hex("2F80ED"), new RectangularPolygon(906, 504, 6, 92));
            ctx.Fill(Hex("2F80ED"), new RectangularPolygon(736, 592, 176, 4));
            ctx.Fill(Hex("8EEAFF"), new RectangularPolygon(68, 574, 42, 3));
            ctx.Fill(Hex("8EEAFF"), new RectangularPolygon(68, 532, 3, 42));
            ctx.Fill(Hex("2F80ED"), new RectangularPolygon(846, 66, 42, 3));
            ctx.Fill(Hex("2F80ED"), new RectangularPolygon(885, 66, 3, 42));
        }

        private static void DrawHeader(IImageProcessingContext ctx, Hunter hunter, string ownerName)
        {
            var labelFont = Font(23, FontStyle.Bold);
            var titleFont = Font(54, FontStyle.Bold);

            ctx.DrawText("HUNTER LICENSE", labelFont, Hex("8EEAFF"), new PointF(78, 78));
            ctx.DrawText(hunter.CharacterName, titleFont, Hex("F4F7FF"), new PointF(76, 120));

            ctx.Fill(Hex("20263A"), new RectangularPolygon(660, 78, 208, 36));
            ctx.Draw(Hex("3B4568"), 1, new RectangularPolygon(660, 78, 208, 36));
            ctx.DrawText("OWNER", Font(14, FontStyle.Bold), Hex("8D93B8"), new PointF(680, 89));
            ctx.DrawText(ownerName, Font(18, FontStyle.Bold), Hex("F4F7FF"), new PointF(746, 85));

            ctx.Fill(Hex("111827"), new RectangularPolygon(660, 128, 208, 36));
            ctx.Draw(Hex("2A3554"), 1, new RectangularPolygon(660, 128, 208, 36));
            ctx.DrawText("STATUS", Font(14, FontStyle.Bold), Hex("8D93B8"), new PointF(680, 139));
            ctx.DrawText("ACTIVE", Font(18, FontStyle.Bold), Hex("72E39A"), new PointF(752, 135));
        }

        private static void DrawHpPanel(IImageProcessingContext ctx, Hunter hunter, double hpRatio)
        {
            ctx.Fill(Hex("0F1422"), new RectangularPolygon(76, 232, 808, 108));
            ctx.Draw(Hex("2E3858"), 1, new RectangularPolygon(76, 232, 808, 108));
            ctx.Fill(Hex("192033"), new RectangularPolygon(76, 232, 808, 4));

            ctx.DrawText("VITAL SIGN", Font(17, FontStyle.Bold), Hex("8EEAFF"), new PointF(100, 254));
            ctx.DrawText($"HP {hunter.CurrentHp} / {hunter.MaxHp}", Font(32, FontStyle.Bold), Hex("F4F7FF"), new PointF(100, 282));
            ctx.DrawText($"{Math.Round(hpRatio * 100)}%", Font(24, FontStyle.Bold), HpColor(hpRatio), new PointF(770, 282));

            ctx.Fill(Hex("252C43"), new RectangularPolygon(278, 289, 452, 24));
            ctx.Fill(HpColor(hpRatio), new RectangularPolygon(278, 289, (float)(452 * hpRatio), 24));
            ctx.Draw(Hex("596282"), 1, new RectangularPolygon(278, 289, 452, 24));

            for (var i = 0; i <= 10; i++)
            {
                var x = 278 + i * 45;
                ctx.Fill(Hex("101523"), new RectangularPolygon(x, 289, 2, 24));
            }
        }

        private static void DrawStats(IImageProcessingContext ctx, Hunter hunter)
        {
            DrawStatColumn(ctx, "PHYSICAL", 76, 376,
                ("STR", "\uADFC\uB825", hunter.Strength, hunter.GetModifier(hunter.Strength)),
                ("DEX", "\uBBFC\uCCA9", hunter.Dexterity, hunter.GetModifier(hunter.Dexterity)),
                ("CON", "\uCCB4\uB825", hunter.Constitution, hunter.GetModifier(hunter.Constitution)));

            DrawStatColumn(ctx, "MENTAL", 502, 376,
                ("INT", "\uC9C0\uB2A5", hunter.Intelligence, hunter.GetModifier(hunter.Intelligence)),
                ("WIS", "\uC9C0\uD61C", hunter.Wisdom, hunter.GetModifier(hunter.Wisdom)),
                ("CHA", "\uB9E4\uB825", hunter.Charisma, hunter.GetModifier(hunter.Charisma)));
        }

        private static void DrawStatColumn(IImageProcessingContext ctx, string title, int x, int y, params (string Code, string Name, int Score, int Modifier)[] stats)
        {
            ctx.DrawText(title, Font(18, FontStyle.Bold), Hex("8EEAFF"), new PointF(x, y));
            ctx.Fill(Hex("0F1422"), new RectangularPolygon(x, y + 32, 382, 154));
            ctx.Draw(Hex("303B5D"), 1, new RectangularPolygon(x, y + 32, 382, 154));
            ctx.Fill(Hex("192033"), new RectangularPolygon(x, y + 32, 382, 4));
            ctx.DrawText("STAT", Font(13, FontStyle.Bold), Hex("7D86A8"), new PointF(x + 24, y + 46));
            ctx.DrawText("VALUE", Font(13, FontStyle.Bold), Hex("7D86A8"), new PointF(x + 204, y + 46));
            ctx.DrawText("MOD", Font(13, FontStyle.Bold), Hex("7D86A8"), new PointF(x + 294, y + 46));
            ctx.Draw(Hex("273251"), 1, new RectangularPolygon(x + 18, y + 66, 346, 1));

            for (var i = 0; i < stats.Length; i++)
            {
                var stat = stats[i];
                var rowY = y + 78 + i * 36;
                ctx.Fill(Hex("20263A"), new RectangularPolygon(x + 20, rowY, 60, 25));
                ctx.Draw(Hex("55607E"), 1, new RectangularPolygon(x + 20, rowY, 60, 25));
                ctx.DrawText(stat.Code, Font(15, FontStyle.Bold), Hex("EAF0FF"), new PointF(x + 34, rowY + 3));

                ctx.DrawText(stat.Name, Font(19, FontStyle.Bold), Hex("F4F7FF"), new PointF(x + 96, rowY));
                ctx.DrawText(stat.Score.ToString(), Font(25, FontStyle.Bold), Hex("FFD166"), new PointF(x + 218, rowY - 3));
                ctx.DrawText(FormatModifier(stat.Modifier), Font(21, FontStyle.Bold), stat.Modifier >= 0 ? Hex("72E39A") : Hex("FF6B81"), new PointF(x + 304, rowY));
            }
        }

        private static void DrawFooter(IImageProcessingContext ctx)
        {
            ctx.Fill(Hex("101523"), new RectangularPolygon(76, 574, 808, 24));
            ctx.Draw(Hex("25304D"), 1, new RectangularPolygon(76, 574, 808, 24));
            ctx.DrawText("PANDORA NETWORK / CLASSIFIED HUNTER RECORD", Font(14, FontStyle.Bold), Hex("7D86A8"), new PointF(96, 578));
            ctx.DrawText(DateTime.Now.ToString("yyyy.MM.dd HH:mm"), Font(14, FontStyle.Bold), Hex("7D86A8"), new PointF(708, 578));
        }

        private static void DrawHudGrid(IImageProcessingContext ctx)
        {
            for (var x = 80; x < Width; x += 80)
            {
                DrawLine(ctx, x, 0, x + 28, Height, Hex("0D1830"), 1);
            }

            for (var y = 90; y < Height; y += 78)
            {
                DrawLine(ctx, 0, y, Width, y - 38, Hex("0D1830"), 1);
            }
        }

        private static string FormatModifier(int modifier)
        {
            return modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        }

        private static Color HpColor(double ratio)
        {
            if (ratio >= 0.7)
            {
                return Hex("72E39A");
            }

            if (ratio >= 0.35)
            {
                return Hex("FFD166");
            }

            return Hex("FF6B81");
        }

        private static void DrawLine(IImageProcessingContext ctx, float x1, float y1, float x2, float y2, Color color, float width)
        {
            var path = new PathBuilder()
                .AddLine(new PointF(x1, y1), new PointF(x2, y2))
                .Build();

            ctx.Draw(color, width, path);
        }

        private static Font Font(float size, FontStyle style)
        {
            foreach (var name in new[] { "Malgun Gothic", "Segoe UI", "Arial" })
            {
                if (SystemFonts.TryGet(name, out var family))
                {
                    return family.CreateFont(size, style);
                }
            }

            return SystemFonts.Collection.Families.First().CreateFont(size, style);
        }

        private static Color Hex(string value)
        {
            return Color.ParseHex(value);
        }

        private static Color HexAlpha(string value, byte alpha)
        {
            var clean = value.TrimStart('#');
            var r = Convert.ToByte(clean[0..2], 16);
            var g = Convert.ToByte(clean[2..4], 16);
            var b = Convert.ToByte(clean[4..6], 16);
            return Color.FromRgba(r, g, b, alpha);
        }

        private static Image<Rgba32>? LoadOrnamentBackground()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "pandora-hud-bg.png"),
                System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Assets", "pandora-hud-bg.png")
            };

            var path = candidates.FirstOrDefault(candidate => System.IO.File.Exists(candidate));
            if (path == null)
            {
                return null;
            }

            var image = Image.Load<Rgba32>(path);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(Width, Height),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));

            return image;
        }
    }
}
