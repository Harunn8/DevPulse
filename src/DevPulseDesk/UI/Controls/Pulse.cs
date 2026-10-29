using System.Drawing.Drawing2D;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>EKG (kalp atışı) dalga formu ve logo çizim yardımcıları.</summary>
    internal static class Pulse
    {
        /// <summary>Bir atım periyodu içinde (0..1) normalize edilmiş EKG genliği (-0.4..1).</summary>
        public static float Ecg(float p)
        {
            p -= MathF.Floor(p);
            return p switch
            {
                < 0.10f => 0f,
                < 0.20f => 0.12f * MathF.Sin(MathF.PI * (p - 0.10f) / 0.10f),
                < 0.26f => 0f,
                < 0.28f => -0.15f * ((p - 0.26f) / 0.02f),
                < 0.31f => -0.15f + 1.15f * ((p - 0.28f) / 0.03f),
                < 0.34f => 1.0f - 1.38f * ((p - 0.31f) / 0.03f),
                < 0.37f => -0.38f + 0.38f * ((p - 0.34f) / 0.03f),
                < 0.48f => 0f,
                < 0.66f => 0.24f * MathF.Sin(MathF.PI * (p - 0.48f) / 0.18f),
                _ => 0f
            };
        }

        /// <summary>
        /// Soldan sağa ilerleyen, arkasında sönümlenen iz bırakan EKG çizgisi.
        /// </summary>
        public static void DrawTrace(Graphics g, RectangleF area, float headX, float beatWidth, float trailLength, Color color, float width = 2.2f)
        {
            var baseline = area.Y + area.Height * 0.62f;
            var amplitude = area.Height * 0.55f;
            const float step = 2.5f;

            PointF? previous = null;
            for (var i = trailLength; i >= 0; i -= step)
            {
                var x = headX - i;
                if (x < area.X || x > area.Right)
                {
                    previous = null;
                    continue;
                }

                var y = baseline - Ecg((x - area.X) / beatWidth) * amplitude;
                var point = new PointF(x, y);
                if (previous is { } prev)
                {
                    var fade = 1f - i / trailLength;
                    var alpha = (int)(MathF.Pow(fade, 1.6f) * 255);
                    using var glow = new Pen(Theme.Alpha(color, alpha / 5), width * 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    using var pen = new Pen(Theme.Alpha(color, alpha), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(glow, prev, point);
                    g.DrawLine(pen, prev, point);
                }
                previous = point;
            }

            if (headX >= area.X && headX <= area.Right)
            {
                var headY = baseline - Ecg((headX - area.X) / beatWidth) * amplitude;
                Theme.RadialLight(g, new PointF(headX, headY), 22, color, 160);
                using var dot = new SolidBrush(Color.White);
                g.FillEllipse(dot, headX - 3, headY - 3, 6, 6);
            }
        }

        /// <summary>Gradyan zeminli, içinde nabız çizgisi olan DevPulse logosu.</summary>
        public static void DrawLogo(Graphics g, RectangleF rect, float time)
        {
            using var path = Theme.RoundRect(rect, rect.Width * 0.28f);
            Theme.Glow(g, rect, rect.Width * 0.28f, Theme.Accent, 0.5f + 0.3f * MathF.Sin(time * 3f), 5, 1.4f);
            using var fill = new LinearGradientBrush(rect, Theme.Accent, Theme.Accent2, 45f);
            g.FillPath(fill, path);

            var pad = rect.Width * 0.16f;
            var line = RectangleF.Inflate(rect, -pad, -rect.Height * 0.22f);
            var points = new List<PointF>();
            for (var x = 0f; x <= 1.0001f; x += 0.02f)
            {
                var p = 0.05f + x * 0.85f;
                points.Add(new PointF(line.X + x * line.Width, line.Y + line.Height * 0.62f - Ecg(p) * line.Height * 0.62f));
            }
            using var pen = new Pen(Color.White, Math.Max(1.6f, rect.Width * 0.065f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, points.ToArray());
        }
    }
}
