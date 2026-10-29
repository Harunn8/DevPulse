using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>Sayacı yumuşak şekilde artan/azalan, hover'da yükselen istatistik kartı.</summary>
    internal sealed class StatCard : CanvasControl
    {
        private readonly SmoothValue _value = new(0, 6);
        private readonly SmoothValue _ratio = new(0, 6);
        private readonly SmoothValue _hover = new(0, 12);
        private readonly SmoothValue _appear = new(0, 5);
        private float _flash;

        public StatCard()
        {
            BackColor = Theme.Background;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Accent { get; set; } = Theme.Accent;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Suffix { get; set; } = "";

        /// <summary>Sayı yerine gösterilecek metin (örn. "—").</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? Placeholder { get; set; }

        public void SetValue(float value, float ratio, string caption)
        {
            if (Math.Abs(value - _value.Target) > 0.001f) _flash = 1f;
            _value.Target = value;
            _ratio.Target = Math.Clamp(ratio, 0, 1);
            Caption = caption;
            StartTicking();
        }

        /// <summary>Kartı sahneye kaydırarak getirir.</summary>
        public void Appear()
        {
            _appear.Snap(0);
            _appear.Target = 1;
            StartTicking();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover.Target = 1;
            StartTicking();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover.Target = 0;
            StartTicking();
        }

        protected override bool OnFrame(float dt)
        {
            _flash = Math.Max(0, _flash - dt * 1.5f);
            return _value.Step(dt) | _ratio.Step(dt) | _hover.Step(dt) | _appear.Step(dt) | _flash > 0;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var appear = _appear.Value;
            if (appear < 0.01f) return;
            var hover = _hover.Value;
            var lift = hover * 3f + (1 - appear) * 18f;
            var card = new RectangleF(6, 6 - lift + 3, size.Width - 12, size.Height - 12);
            var alpha = appear;

            // Gölge
            using (var shadowPath = Theme.RoundRect(new RectangleF(card.X + 2, card.Y + 6, card.Width - 4, card.Height), 16))
            using (var shadow = new SolidBrush(Color.FromArgb((int)(60 * alpha * (0.5f + hover)), 0, 0, 0)))
            {
                g.FillPath(shadow, shadowPath);
            }

            using var path = Theme.RoundRect(card, 16);
            using (var fill = new LinearGradientBrush(card, Theme.Alpha(Theme.Lerp(Theme.Surface, Theme.SurfaceAlt, hover * 0.5f), alpha), Theme.Alpha(Theme.Surface, alpha), 90f))
                g.FillPath(fill, path);

            // Köşe ışığı
            g.SetClip(path);
            Theme.RadialLight(g, new PointF(card.Right - 20, card.Y + 10), 110, Accent, (int)((26 + hover * 30 + _flash * 60) * alpha));
            using (var watermark = new SolidBrush(Theme.Alpha(Accent, (int)(14 * alpha))))
                g.DrawString(Glyph, Theme.Icon(64f), watermark, card.Right - 82, card.Bottom - 70);
            g.ResetClip();

            using (var border = new Pen(Theme.Alpha(Theme.Lerp(Theme.Border, Accent, hover * 0.5f + _flash * 0.4f), alpha), 1f))
                g.DrawPath(border, path);

            // Simge rozeti
            var iconRect = new RectangleF(card.X + 18, card.Y + 18, 38, 38);
            using (var iconPath = Theme.RoundRect(iconRect, 11))
            using (var iconFill = new SolidBrush(Theme.Alpha(Accent, (int)(34 * alpha))))
            {
                g.FillPath(iconFill, iconPath);
            }
            using (var iconBrush = new SolidBrush(Theme.Alpha(Accent, alpha)))
                g.DrawString(Glyph, Theme.Icon(17f), iconBrush, iconRect, Theme.Center);

            using (var titleBrush = new SolidBrush(Theme.Alpha(Theme.TextSecondary, alpha)))
                g.DrawString(Title, Theme.Font(13.5f), titleBrush, new RectangleF(iconRect.Right + 12, iconRect.Y, card.Width - 90, iconRect.Height), Theme.LeftCenter);

            var valueText = Placeholder ?? $"{Math.Round(_value.Value):0}{Suffix}";
            using (var valueBrush = new SolidBrush(Theme.Alpha(Theme.TextPrimary, alpha)))
                g.DrawString(valueText, Theme.SemiBold(30f), valueBrush, card.X + 16, card.Y + 60);

            using (var captionBrush = new SolidBrush(Theme.Alpha(Theme.TextMuted, alpha)))
                g.DrawString(Caption, Theme.Font(12f), captionBrush, new RectangleF(card.X + 18, card.Bottom - 30, card.Width - 36, 18), Theme.LeftCenter);

            // Oran çubuğu
            var bar = new RectangleF(card.X + 18, card.Bottom - 10, card.Width - 36, 3);
            using (var trackPath = Theme.RoundRect(bar, 1.5f))
            using (var track = new SolidBrush(Theme.Alpha(Theme.Border, alpha)))
            {
                g.FillPath(track, trackPath);
            }
            if (_ratio.Value > 0.005f)
            {
                var fillRect = new RectangleF(bar.X, bar.Y, Math.Max(3, bar.Width * _ratio.Value), bar.Height);
                using var fillPath = Theme.RoundRect(fillRect, 1.5f);
                using var fill = new LinearGradientBrush(RectangleF.Inflate(fillRect, 1, 0), Theme.Alpha(Accent, alpha), Theme.Alpha(Theme.Lerp(Accent, Color.White, 0.3f), alpha), 0f);
                g.FillPath(fill, fillPath);
            }
        }
    }
}
