using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DevPulseDesk.UI
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(10, 13, 24);
        public static readonly Color Chrome = Color.FromArgb(13, 17, 31);
        public static readonly Color Surface = Color.FromArgb(18, 24, 41);
        public static readonly Color SurfaceAlt = Color.FromArgb(25, 32, 54);
        public static readonly Color SurfaceHover = Color.FromArgb(32, 41, 68);
        public static readonly Color Border = Color.FromArgb(38, 48, 76);
        public static readonly Color Terminal = Color.FromArgb(8, 11, 20);

        public static readonly Color Accent = Color.FromArgb(0, 212, 255);
        public static readonly Color Accent2 = Color.FromArgb(124, 92, 255);
        public static readonly Color Accent3 = Color.FromArgb(255, 79, 216);
        public static readonly Color Success = Color.FromArgb(46, 229, 157);
        public static readonly Color Danger = Color.FromArgb(255, 77, 109);
        public static readonly Color Warning = Color.FromArgb(255, 181, 71);

        public static readonly Color TextPrimary = Color.FromArgb(234, 238, 247);
        public static readonly Color TextSecondary = Color.FromArgb(140, 150, 176);
        public static readonly Color TextMuted = Color.FromArgb(88, 98, 128);

        private static readonly Dictionary<(string, float, FontStyle), Font> FontCache = new();
        private static HashSet<string>? _installedFamilies;

        private static readonly Lazy<string> UiFamily = new(() => Resolve("Segoe UI Variable Text", "Segoe UI", "Arial"));
        private static readonly Lazy<string> DisplayFamily = new(() => Resolve("Segoe UI Variable Display", "Segoe UI", "Arial"));
        private static readonly Lazy<string?> SemiboldFamily = new(() => ResolveOptional("Segoe UI Semibold"));
        private static readonly Lazy<string?> LightFamily = new(() => ResolveOptional("Segoe UI Light"));
        private static readonly Lazy<string> MonoFamily = new(() => Resolve("Cascadia Mono", "Cascadia Code", "Consolas", "Courier New"));
        private static readonly Lazy<string> IconFamily = new(() => Resolve("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol"));

        public static readonly StringFormat Center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        public static readonly StringFormat LeftCenter = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        public static readonly StringFormat RightCenter = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        public static readonly StringFormat TopLeft = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };

        /// <summary>Mantıksal piksel cinsinden font (ölçekli Graphics üzerinde kullanılır).</summary>
        public static Font Font(float px, FontStyle style = FontStyle.Regular) => Get(UiFamily.Value, px, style);

        public static Font Display(float px, FontStyle style = FontStyle.Regular) => Get(DisplayFamily.Value, px, style);

        public static Font SemiBold(float px) =>
            SemiboldFamily.Value != null ? Get(SemiboldFamily.Value, px, FontStyle.Regular) : Get(UiFamily.Value, px, FontStyle.Bold);

        public static Font Light(float px) =>
            LightFamily.Value != null ? Get(LightFamily.Value, px, FontStyle.Regular) : Get(DisplayFamily.Value, px, FontStyle.Regular);

        public static Font Mono(float px, FontStyle style = FontStyle.Regular) => Get(MonoFamily.Value, px, style);

        public static Font Icon(float px) => Get(IconFamily.Value, px, FontStyle.Regular);

        /// <summary>Gerçek (DPI ile ölçeklenmiş) piksel cinsinden font; yerel kontroller (TextBox vb.) için.</summary>
        public static Font Native(float px, FontStyle style = FontStyle.Regular) => Get(UiFamily.Value, px * Dpi.Scale, style);

        private static Font Get(string family, float px, FontStyle style)
        {
            var key = (family, px, style);
            if (!FontCache.TryGetValue(key, out var font))
            {
                font = new Font(family, px, style, GraphicsUnit.Pixel);
                FontCache[key] = font;
            }
            return font;
        }

        private static string Resolve(params string[] candidates) => ResolveOptional(candidates) ?? SystemFonts.DefaultFont.FontFamily.Name;

        private static string? ResolveOptional(params string[] candidates)
        {
            _installedFamilies ??= new HashSet<string>(new InstalledFontCollection().Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            return candidates.FirstOrDefault(_installedFamilies.Contains);
        }

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            radius = Math.Max(0.1f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2f));
            var d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Alpha(Color c, float alpha01) => Color.FromArgb(Math.Clamp((int)(alpha01 * 255), 0, 255), c.R, c.G, c.B);

        public static Color Alpha(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Yuvarlak dikdörtgen etrafında yumuşak bir parıltı çizer.</summary>
        public static void Glow(Graphics g, RectangleF r, float radius, Color color, float intensity, int layers = 6, float spread = 1.6f)
        {
            if (intensity <= 0.01f) return;
            for (var i = layers; i >= 1; i--)
            {
                var inflate = i * spread;
                var rect = RectangleF.Inflate(r, inflate, inflate);
                using var path = RoundRect(rect, radius + inflate);
                using var pen = new Pen(Alpha(color, (int)(intensity * 70f / (i + 1))), spread * 1.2f);
                g.DrawPath(pen, path);
            }
        }

        /// <summary>Merkezden dışa doğru sönen dairesel ışık.</summary>
        public static void RadialLight(Graphics g, PointF center, float radius, Color color, int alpha)
        {
            if (radius < 1 || alpha <= 0) return;
            using var path = new GraphicsPath();
            path.AddEllipse(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Alpha(color, alpha),
                SurroundColors = [Alpha(color, 0)],
                CenterPoint = center
            };
            g.FillPath(brush, path);
        }

        public static string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            var initials = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name.Trim()[..Math.Min(2, name.Trim().Length)];
            return initials.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        }
    }

    /// <summary>Segoe Fluent Icons / MDL2 Assets glifleri.</summary>
    internal static class Glyph
    {
        public const string User = "";
        public const string Lock = "";
        public const string Globe = "";
        public const string Play = "";
        public const string Stop = "";
        public const string Refresh = "";
        public const string Devices = "";
        public const string Check = "";
        public const string Cancel = "";
        public const string Close = "";
        public const string Minimize = "";
        public const string Maximize = "";
        public const string Restore = "";
        public const string Search = "";
        public const string Clock = "";
        public const string Sync = "";
        public const string Warning = "";
        public const string Info = "";
        public const string Power = "";
        public const string Wifi = "";
        public const string Ethernet = "";
        public const string View = "";
        public const string Hide = "";
        public const string Shield = "";
        public const string Speed = "";
        public const string Filter = "";
        public const string Home = "";
        public const string Flash = "";
        public const string Report = "";
    }
}
