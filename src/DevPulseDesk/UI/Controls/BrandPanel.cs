using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Login ekranının sol tarafı: yüzen ışık küreleri, fareye tepki veren parçacık ağı,
    /// canlı EKG nabız çizgisi ve dönen özellik başlıkları.
    /// </summary>
    internal sealed class BrandPanel : CanvasControl
    {
        private static readonly string[] Features =
        [
            "Ağdaki tüm cihazlarınızın nabzını tek ekrandan tutun.",
            "ICMP ve TCP katmanlarında çok aşamalı bağlantı testleri.",
            "Paralel test motoru ile saniyeler içinde sonuç.",
            "Canlı günlük akışı ile her adımı anbean takip edin."
        ];

        private readonly List<Particle> _particles = new();
        private readonly Random _random = new(7);
        private PointF? _mouse;
        private float _time;
        private float _ecgHead;
        private int _featureIndex;
        private float _featureTime;

        protected override bool AlwaysAnimate => true;

        public BrandPanel()
        {
            BackColor = Theme.Background;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            EnsureParticles();
        }

        private void EnsureParticles()
        {
            var size = Logical;
            if (size.Width < 10 || _particles.Count > 0) return;
            for (var i = 0; i < 58; i++)
            {
                _particles.Add(new Particle
                {
                    X = (float)_random.NextDouble() * size.Width,
                    Y = (float)_random.NextDouble() * size.Height,
                    Vx = ((float)_random.NextDouble() - 0.5f) * 18f,
                    Vy = ((float)_random.NextDouble() - 0.5f) * 18f,
                    Radius = 1f + (float)_random.NextDouble() * 1.6f,
                    Phase = (float)_random.NextDouble() * 6.28f
                });
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = ToLogical(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouse = null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Native.DragWindow(FindForm());
        }

        protected override bool OnFrame(float dt)
        {
            var size = Logical;
            _time += dt;
            _ecgHead += dt * 190f;
            if (_ecgHead > size.Width + 260) _ecgHead = -40;

            _featureTime += dt;
            if (_featureTime > 4.2f)
            {
                _featureTime = 0;
                _featureIndex = (_featureIndex + 1) % Features.Length;
            }

            foreach (var p in _particles)
            {
                if (_mouse is { } m)
                {
                    var dx = m.X - p.X;
                    var dy = m.Y - p.Y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist is < 160 and > 1)
                    {
                        // Fareye hafifçe çekim
                        p.Vx += dx / dist * 14f * dt;
                        p.Vy += dy / dist * 14f * dt;
                    }
                }

                var speed = MathF.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                if (speed > 22f)
                {
                    p.Vx *= 22f / speed;
                    p.Vy *= 22f / speed;
                }

                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (p.X < -10) p.X = size.Width + 10;
                if (p.X > size.Width + 10) p.X = -10;
                if (p.Y < -10) p.Y = size.Height + 10;
                if (p.Y > size.Height + 10) p.Y = -10;
            }
            return true;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var bounds = new RectangleF(0, 0, size.Width, size.Height);
            using (var bg = new LinearGradientBrush(bounds, Color.FromArgb(14, 18, 36), Color.FromArgb(6, 8, 16), 90f))
                g.FillRectangle(bg, bounds);

            // Yüzen ışık küreleri
            Theme.RadialLight(g, new PointF(size.Width * 0.25f + MathF.Sin(_time * 0.35f) * 50, size.Height * 0.30f + MathF.Cos(_time * 0.28f) * 40), size.Width * 0.55f, Theme.Accent2, 70);
            Theme.RadialLight(g, new PointF(size.Width * 0.85f + MathF.Cos(_time * 0.31f) * 40, size.Height * 0.70f + MathF.Sin(_time * 0.4f) * 50), size.Width * 0.5f, Theme.Accent, 45);
            Theme.RadialLight(g, new PointF(size.Width * 0.15f + MathF.Sin(_time * 0.22f + 2) * 30, size.Height * 0.92f), size.Width * 0.35f, Theme.Accent3, 30);

            // Hafif ızgara
            using (var gridPen = new Pen(Color.FromArgb(9, 255, 255, 255), 1f))
            {
                for (var x = 0f; x < size.Width; x += 36) g.DrawLine(gridPen, x, 0, x, size.Height);
                for (var y = 0f; y < size.Height; y += 36) g.DrawLine(gridPen, 0, y, size.Width, y);
            }

            DrawNetwork(g);

            // Logo
            Pulse.DrawLogo(g, new RectangleF(48, 46, 42, 42), _time);
            using (var white = new SolidBrush(Theme.TextPrimary))
                g.DrawString("DevPulse", Theme.SemiBold(24f), white, 100, 48);
            using (var muted = new SolidBrush(Theme.TextSecondary))
                g.DrawString("DEVICE HEALTH MONITOR", Theme.Font(10.5f, FontStyle.Bold), muted, 102, 77);

            // Başlık
            var headlineY = size.Height * 0.36f;
            using (var white = new SolidBrush(Theme.TextPrimary))
                g.DrawString("Cihaz sağlığını", Theme.Light(40f), white, 44, headlineY);
            var line2 = new RectangleF(44, headlineY + 50, size.Width - 60, 60);
            var shift = (MathF.Sin(_time * 0.8f) + 1) / 2f;
            using (var gradient = new LinearGradientBrush(new RectangleF(44 - 200 * shift, line2.Y, size.Width, 60), Theme.Accent, Theme.Accent3, 0f)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = [Theme.Accent, Theme.Accent2, Theme.Accent3],
                    Positions = [0f, 0.5f, 1f]
                }
            })
            {
                g.DrawString("gerçek zamanlı izleyin.", Theme.SemiBold(40f), gradient, line2.X, line2.Y);
            }

            DrawFeature(g, size, headlineY + 128);

            // EKG nabız çizgisi
            var ecgArea = new RectangleF(0, size.Height * 0.70f, size.Width, 90);
            using (var baseline = new Pen(Color.FromArgb(18, 0, 212, 255), 1f))
                g.DrawLine(baseline, 0, ecgArea.Y + ecgArea.Height * 0.62f, size.Width, ecgArea.Y + ecgArea.Height * 0.62f);
            Pulse.DrawTrace(g, ecgArea, _ecgHead, 230f, 260f, Theme.Accent);

            DrawFooter(g, size);
        }

        private void DrawNetwork(Graphics g)
        {
            const float linkDistance = 105f;
            for (var i = 0; i < _particles.Count; i++)
            {
                var a = _particles[i];
                for (var j = i + 1; j < _particles.Count; j++)
                {
                    var b = _particles[j];
                    var dx = a.X - b.X;
                    var dy = a.Y - b.Y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 > linkDistance * linkDistance) continue;
                    var alpha = (int)((1 - MathF.Sqrt(d2) / linkDistance) * 55);
                    using var pen = new Pen(Theme.Alpha(Theme.Accent, alpha), 1f);
                    g.DrawLine(pen, a.X, a.Y, b.X, b.Y);
                }

                if (_mouse is { } m)
                {
                    var dx = a.X - m.X;
                    var dy = a.Y - m.Y;
                    var d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < 150)
                    {
                        using var pen = new Pen(Theme.Alpha(Theme.Accent2, (int)((1 - d / 150) * 110)), 1f);
                        g.DrawLine(pen, a.X, a.Y, m.X, m.Y);
                    }
                }
            }

            foreach (var p in _particles)
            {
                var twinkle = 0.55f + 0.45f * MathF.Sin(_time * 2f + p.Phase);
                using var brush = new SolidBrush(Theme.Alpha(Theme.Accent, (int)(200 * twinkle)));
                g.FillEllipse(brush, p.X - p.Radius, p.Y - p.Radius, p.Radius * 2, p.Radius * 2);
            }
        }

        private void DrawFeature(Graphics g, SizeF size, float y)
        {
            // 0-0.5 sn giriş, 3.7-4.2 sn çıkış
            var enter = (float)Easing.OutCubic(Math.Clamp(_featureTime / 0.5f, 0, 1));
            var exit = (float)Easing.InCubic(Math.Clamp((_featureTime - 3.7f) / 0.5f, 0, 1));
            var alpha = enter * (1 - exit);
            var offset = (1 - enter) * 14f - exit * 14f;

            using var brush = new SolidBrush(Theme.Alpha(Theme.TextSecondary, alpha));
            g.DrawString(Features[_featureIndex], Theme.Font(15.5f), brush, new RectangleF(46, y + offset, size.Width - 90, 48));

            // İlerleme noktaları
            for (var i = 0; i < Features.Length; i++)
            {
                var active = i == _featureIndex;
                var w = active ? 26f : 8f;
                var x = 46 + i * 16 + (i > _featureIndex ? 18 : 0);
                using var path = Theme.RoundRect(new RectangleF(x, y + 58, w, 6), 3);
                if (active)
                {
                    var progress = Math.Clamp(_featureTime / 4.2f, 0, 1);
                    using var track = new SolidBrush(Color.FromArgb(50, 255, 255, 255));
                    g.FillPath(track, path);
                    using var fillPath = Theme.RoundRect(new RectangleF(x, y + 58, Math.Max(6, w * progress), 6), 3);
                    using var fill = new LinearGradientBrush(new RectangleF(x, y + 58, w + 1, 6), Theme.Accent, Theme.Accent2, 0f);
                    g.FillPath(fill, fillPath);
                }
                else
                {
                    using var dot = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                    g.FillPath(dot, path);
                }
            }
        }

        private void DrawFooter(Graphics g, SizeF size)
        {
            var y = size.Height - 46;
            var blink = 0.5f + 0.5f * MathF.Sin(_time * 4f);
            Theme.RadialLight(g, new PointF(52, y + 9), 10, Theme.Success, (int)(140 * blink));
            using (var dot = new SolidBrush(Theme.Success)) g.FillEllipse(dot, 48, y + 5, 8, 8);
            using (var brush = new SolidBrush(Theme.TextSecondary))
                g.DrawString("Tüm sistemler çalışıyor", Theme.Font(12.5f), brush, 62, y);
            using (var muted = new SolidBrush(Theme.TextMuted))
                g.DrawString($"v1.0  •  © {DateTime.Now.Year} DevPulse", Theme.Font(12f), muted, new RectangleF(0, y, size.Width - 40, 20), new StringFormat { Alignment = StringAlignment.Far });
        }

        private sealed class Particle
        {
            public float X, Y, Vx, Vy, Radius, Phase;
        }
    }
}
