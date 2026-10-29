using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Başarılı girişten sonra tüm pencereyi kaplayan karşılama animasyonu:
    /// halka çizimi → avatar → onay rozeti + konfeti → "Hoş geldin, kullanıcı" daktilo efekti → yükleme çubuğu.
    /// </summary>
    internal sealed class WelcomeOverlay : CanvasControl
    {
        private const float CharDelay = 0.055f;

        private readonly List<Confetti> _confetti = new();
        private readonly Random _random = new();
        private float _t;
        private bool _burst;
        private bool _finished;

        protected override bool AlwaysAnimate => true;

        public event EventHandler? Finished;

        /// <summary>Kaplamanın altında solarak kaybolan önceki ekran görüntüsü.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap? Snapshot { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string UserName { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get; set; } = "Çalışma alanınız hazırlanıyor";

        private float TypingStart => 1.25f;
        private float TypingEnd => TypingStart + UserName.Length * CharDelay;
        private float TotalDuration => Math.Max(3.4f, TypingEnd + 1.9f);

        private static float Phase(float t, float start, float duration) => Math.Clamp((t - start) / duration, 0f, 1f);

        protected override bool OnFrame(float dt)
        {
            _t += dt;

            if (!_burst && _t > 1.05f)
            {
                _burst = true;
                var colors = new[] { Theme.Accent, Theme.Accent2, Theme.Accent3, Theme.Success, Theme.Warning };
                for (var i = 0; i < 46; i++)
                {
                    var angle = (float)(_random.NextDouble() * Math.PI * 2);
                    var speed = 120f + (float)_random.NextDouble() * 260f;
                    _confetti.Add(new Confetti
                    {
                        Vx = MathF.Cos(angle) * speed,
                        Vy = MathF.Sin(angle) * speed - 60f,
                        Size = 3f + (float)_random.NextDouble() * 4f,
                        Color = colors[_random.Next(colors.Length)],
                        Spin = (float)_random.NextDouble() * 360f,
                        SpinSpeed = ((float)_random.NextDouble() - 0.5f) * 720f
                    });
                }
            }

            for (var i = _confetti.Count - 1; i >= 0; i--)
            {
                var c = _confetti[i];
                c.Age += dt;
                c.Vy += 260f * dt;
                c.Vx *= 1f - 1.6f * dt;
                c.X += c.Vx * dt;
                c.Y += c.Vy * dt;
                c.Spin += c.SpinSpeed * dt;
                if (c.Age > 1.6f) _confetti.RemoveAt(i);
            }

            if (!_finished && _t >= TotalDuration)
            {
                _finished = true;
                Finished?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var center = new PointF(size.Width / 2f, size.Height * 0.40f);
            var bgIn = (float)Easing.OutCubic(Phase(_t, 0f, 0.6f));

            // Önceki ekranı hafifçe büyüterek soldur
            if (Snapshot != null && bgIn < 1f)
            {
                var zoom = 1f + bgIn * 0.06f;
                var w = size.Width * zoom;
                var h = size.Height * zoom;
                g.DrawImage(Snapshot, new RectangleF((size.Width - w) / 2f, (size.Height - h) / 2f, w, h));
            }

            var bounds = new RectangleF(0, 0, size.Width, size.Height);
            using (var bg = new LinearGradientBrush(bounds, Theme.Alpha(Color.FromArgb(14, 18, 38), bgIn), Theme.Alpha(Color.FromArgb(5, 7, 14), bgIn), 90f))
                g.FillRectangle(bg, bounds);

            Theme.RadialLight(g, new PointF(center.X - 160 + MathF.Sin(_t * 0.6f) * 40, center.Y), 380, Theme.Accent2, (int)(70 * bgIn));
            Theme.RadialLight(g, new PointF(center.X + 180, center.Y + 120 + MathF.Cos(_t * 0.5f) * 30), 340, Theme.Accent, (int)(45 * bgIn));

            // Genişleyen nabız halkaları
            if (_t > 0.9f)
            {
                for (var i = 0; i < 3; i++)
                {
                    var p = ((_t - 0.9f) * 0.55f + i / 3f) % 1f;
                    var r = 70 + p * 170;
                    using var pen = new Pen(Theme.Alpha(Theme.Accent, (int)((1 - p) * 70 * bgIn)), 1.5f);
                    g.DrawEllipse(pen, center.X - r, center.Y - r, r * 2, r * 2);
                }
            }

            DrawAvatar(g, center);
            DrawConfetti(g, center);
            DrawTexts(g, size, center);
        }

        private void DrawAvatar(Graphics g, PointF center)
        {
            const float radius = 62f;
            var ring = (float)Easing.InOutCubic(Phase(_t, 0.25f, 0.85f));
            if (ring > 0)
            {
                var rect = new RectangleF(center.X - radius - 10, center.Y - radius - 10, (radius + 10) * 2, (radius + 10) * 2);
                using var track = new Pen(Color.FromArgb(28, 255, 255, 255), 3f);
                g.DrawEllipse(track, rect);
                using var brush = new LinearGradientBrush(rect, Theme.Accent, Theme.Accent3, 45f);
                using var glowBrush = new LinearGradientBrush(rect, Theme.Alpha(Theme.Accent, 60), Theme.Alpha(Theme.Accent3, 60), 45f);
                using var glow = new Pen(glowBrush, 10f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var pen = new Pen(brush, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(glow, rect, -90 + _t * 40, 360 * ring);
                g.DrawArc(pen, rect, -90 + _t * 40, 360 * ring);
            }

            var pop = (float)Easing.OutBack(Phase(_t, 0.55f, 0.6f));
            if (pop > 0.01f)
            {
                var r = radius * pop;
                var avatar = new RectangleF(center.X - r, center.Y - r, r * 2, r * 2);
                Theme.RadialLight(g, center, r * 1.6f, Theme.Accent2, 90);
                using var fill = new LinearGradientBrush(avatar, Theme.Accent, Theme.Accent2, 45f);
                g.FillEllipse(fill, avatar);

                var state = g.Save();
                g.TranslateTransform(center.X, center.Y);
                g.ScaleTransform(pop, pop);
                using var white = new SolidBrush(Color.White);
                g.DrawString(Theme.Initials(UserName), Theme.SemiBold(40f), white, new RectangleF(-radius, -radius, radius * 2, radius * 2), Theme.Center);
                g.Restore(state);
            }

            // Onay rozeti
            var badge = (float)Easing.OutBack(Phase(_t, 1.0f, 0.45f));
            if (badge > 0.01f)
            {
                var bc = new PointF(center.X + radius * 0.72f, center.Y + radius * 0.72f);
                var br = 17f * badge;
                using (var ringBrush = new SolidBrush(Color.FromArgb(12, 16, 32))) g.FillEllipse(ringBrush, bc.X - br - 4, bc.Y - br - 4, (br + 4) * 2, (br + 4) * 2);
                using (var fill = new SolidBrush(Theme.Success)) g.FillEllipse(fill, bc.X - br, bc.Y - br, br * 2, br * 2);

                var draw = (float)Easing.OutCubic(Phase(_t, 1.15f, 0.35f));
                if (draw > 0)
                {
                    var p1 = new PointF(bc.X - 7, bc.Y);
                    var p2 = new PointF(bc.X - 2, bc.Y + 5);
                    var p3 = new PointF(bc.X + 8, bc.Y - 6);
                    using var pen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    if (draw < 0.4f)
                    {
                        var k = draw / 0.4f;
                        g.DrawLine(pen, p1, new PointF(p1.X + (p2.X - p1.X) * k, p1.Y + (p2.Y - p1.Y) * k));
                    }
                    else
                    {
                        var k = (draw - 0.4f) / 0.6f;
                        g.DrawLines(pen, [p1, p2, new PointF(p2.X + (p3.X - p2.X) * k, p2.Y + (p3.Y - p2.Y) * k)]);
                    }
                }
            }
        }

        private void DrawConfetti(Graphics g, PointF center)
        {
            foreach (var c in _confetti)
            {
                var alpha = 1f - Math.Clamp((c.Age - 0.8f) / 0.8f, 0, 1);
                var state = g.Save();
                g.TranslateTransform(center.X + c.X, center.Y + c.Y);
                g.RotateTransform(c.Spin);
                using var brush = new SolidBrush(Theme.Alpha(c.Color, alpha));
                g.FillRectangle(brush, -c.Size / 2, -c.Size / 4, c.Size, c.Size / 2);
                g.Restore(state);
            }
        }

        private void DrawTexts(Graphics g, SizeF size, PointF center)
        {
            var greet = (float)Easing.OutCubic(Phase(_t, 0.95f, 0.5f));
            var textY = center.Y + 108;
            if (greet > 0)
            {
                using var brush = new SolidBrush(Theme.Alpha(Theme.TextSecondary, greet));
                g.DrawString("Hoş geldin,", Theme.Light(26f), brush, new RectangleF(0, textY + (1 - greet) * 16, size.Width, 36), Theme.Center);
            }

            // Kullanıcı adı – daktilo efekti
            var visibleChars = (int)Math.Clamp((_t - TypingStart) / CharDelay, 0, UserName.Length);
            if (visibleChars > 0 || _t > TypingStart)
            {
                var font = Theme.SemiBold(44f);
                var full = UserName;
                var fullSize = g.MeasureString(full, font);
                var shown = full[..visibleChars];
                var x = (size.Width - fullSize.Width) / 2f;
                var nameRect = new RectangleF(x, textY + 38, fullSize.Width + 10, fullSize.Height);

                if (shown.Length > 0)
                {
                    using var gradient = new LinearGradientBrush(new RectangleF(nameRect.X, nameRect.Y, Math.Max(1, nameRect.Width), nameRect.Height), Theme.Accent, Theme.Accent3, 0f)
                    {
                        InterpolationColors = new ColorBlend { Colors = [Theme.Accent, Theme.Accent2, Theme.Accent3], Positions = [0f, 0.5f, 1f] }
                    };
                    g.DrawString(shown, font, gradient, nameRect.X, nameRect.Y);
                }

                // Yanıp sönen imleç
                var typedWidth = shown.Length > 0 ? g.MeasureString(shown, font).Width - 10 : 0;
                var cursorOn = _t < TypingEnd + 0.1f || ((int)(_t * 2.5f) % 2 == 0);
                if (cursorOn && _t < TypingEnd + 1.2f)
                {
                    using var cursor = new SolidBrush(Theme.Accent);
                    g.FillRectangle(cursor, nameRect.X + Math.Max(0, typedWidth) + 6, nameRect.Y + 10, 3, fullSize.Height - 20);
                }
            }

            // Alt başlık ve yükleme çubuğu
            var sub = (float)Easing.OutCubic(Phase(_t, TypingEnd + 0.15f, 0.5f));
            if (sub > 0)
            {
                var dots = new string('.', (int)(_t * 3) % 4);
                using var brush = new SolidBrush(Theme.Alpha(Theme.TextSecondary, sub));
                g.DrawString(Subtitle + dots, Theme.Font(14.5f), brush, new RectangleF(0, textY + 108 + (1 - sub) * 10, size.Width, 24), Theme.Center);

                var barWidth = 260f;
                var bar = new RectangleF((size.Width - barWidth) / 2f, textY + 144, barWidth, 4);
                using var trackPath = Theme.RoundRect(bar, 2);
                using (var track = new SolidBrush(Theme.Alpha(Color.White, 0.08f * sub))) g.FillPath(track, trackPath);

                var progress = (float)Easing.InOutCubic(Phase(_t, TypingEnd + 0.2f, TotalDuration - TypingEnd - 0.35f));
                if (progress > 0.01f)
                {
                    var fillRect = new RectangleF(bar.X, bar.Y, Math.Max(4, bar.Width * progress), bar.Height);
                    using var fillPath = Theme.RoundRect(fillRect, 2);
                    using var fill = new LinearGradientBrush(RectangleF.Inflate(bar, 1, 0), Theme.Accent, Theme.Accent2, 0f);
                    g.FillPath(fill, fillPath);
                    Theme.RadialLight(g, new PointF(fillRect.Right, fillRect.Y + 2), 14, Theme.Accent2, 160);
                }
            }
        }

        private sealed class Confetti
        {
            public float X, Y, Vx, Vy, Size, Spin, SpinSpeed, Age;
            public Color Color;
        }
    }
}
