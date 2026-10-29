using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Gradyan dolgulu, hover'da parlayan, tıklamada dalga (ripple) yayan ve yükleniyor durumuna geçebilen buton.
    /// </summary>
    internal sealed class GlowButton : CanvasControl
    {
        private const float Inset = 5f;

        private readonly SmoothValue _hover = new(0, 14);
        private readonly SmoothValue _press = new(0, 22);
        private readonly SmoothValue _loading = new(0, 10);
        private readonly SmoothValue _colorShift = new(0, 8);
        private readonly List<Ripple> _ripples = new();
        private float _shine = -1f;
        private float _spin;
        private bool _isLoading;
        private Color _colorA = Theme.Accent;
        private Color _colorB = Theme.Accent2;
        private Color _fromA = Theme.Accent;
        private Color _fromB = Theme.Accent2;

        public GlowButton()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = "";

        /// <summary>Saydam, yalnızca kenarlığı gradyan olan ikincil buton görünümü.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Ghost { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Radius { get; set; } = 12f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                _loading.Target = value ? 1 : 0;
                StartTicking();
            }
        }

        /// <summary>Buton renklerini yumuşak geçişle değiştirir.</summary>
        public void SetColors(Color a, Color b)
        {
            _fromA = CurrentA;
            _fromB = CurrentB;
            _colorA = a;
            _colorB = b;
            _colorShift.Snap(0);
            _colorShift.Target = 1;
            StartTicking();
        }

        private Color CurrentA => Theme.Lerp(_fromA, _colorA, _colorShift.Value);
        private Color CurrentB => Theme.Lerp(_fromB, _colorB, _colorShift.Value);

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover.Target = 1;
            _shine = 0;
            StartTicking();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover.Target = 0;
            _press.Target = 0;
            StartTicking();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _isLoading) return;
            _press.Target = 1;
            _ripples.Add(new Ripple(ToLogical(e.Location)));
            StartTicking();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _press.Target = 0;
            StartTicking();
        }

        protected override void OnClick(EventArgs e)
        {
            if (_isLoading || !Enabled) return;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override bool OnFrame(float dt)
        {
            var moving = _hover.Step(dt) | _press.Step(dt) | _loading.Step(dt) | _colorShift.Step(dt);

            for (var i = _ripples.Count - 1; i >= 0; i--)
            {
                _ripples[i].Age += dt;
                if (_ripples[i].Age > 0.7f) _ripples.RemoveAt(i);
            }

            if (_shine >= 0)
            {
                _shine += dt * 1.4f;
                if (_shine > 1.3f) _shine = -1;
            }

            if (_isLoading || _loading.Value > 0.01f) _spin = (_spin + dt * 400f) % 360f;

            return moving || _ripples.Count > 0 || _shine >= 0 || _isLoading;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var hover = _hover.Value;
            var press = _press.Value;
            var enabledAlpha = Enabled ? 1f : 0.45f;
            var a = CurrentA;
            var b = CurrentB;

            var body = new RectangleF(Inset, Inset, size.Width - Inset * 2, size.Height - Inset * 2);
            body.Inflate(-press * 1.5f, -press * 1.5f);
            using var path = Theme.RoundRect(body, Radius);

            // Dış parıltı
            Theme.Glow(g, body, Radius, a, (Ghost ? 0.25f : 0.45f + hover * 0.55f) * enabledAlpha, 4, 1.2f);

            if (Ghost)
            {
                using var fill = new SolidBrush(Theme.Alpha(Theme.Lerp(Theme.SurfaceAlt, a, hover * 0.12f), enabledAlpha));
                g.FillPath(fill, path);
                using var borderBrush = new LinearGradientBrush(body, Theme.Alpha(a, enabledAlpha * (0.55f + hover * 0.45f)), Theme.Alpha(b, enabledAlpha * (0.55f + hover * 0.45f)), 0f);
                using var pen = new Pen(borderBrush, 1.3f);
                g.DrawPath(pen, path);
            }
            else
            {
                using var fill = new LinearGradientBrush(body,
                    Theme.Alpha(Theme.Lerp(a, Color.White, hover * 0.12f), enabledAlpha),
                    Theme.Alpha(Theme.Lerp(b, Color.White, hover * 0.12f), enabledAlpha), 0f);
                g.FillPath(fill, path);

                // Üst yarıda cam efekti
                var gloss = new RectangleF(body.X, body.Y, body.Width, body.Height / 2f);
                using var glossBrush = new LinearGradientBrush(gloss, Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f);
                g.SetClip(path);
                g.FillRectangle(glossBrush, gloss);
                g.ResetClip();
            }

            // Hover'da üzerinden geçen ışık bandı
            if (_shine >= 0 && !_isLoading)
            {
                g.SetClip(path);
                var x = body.X - 60 + (body.Width + 120) * _shine;
                var band = new RectangleF(x - 30, body.Y, 60, body.Height);
                using var shineBrush = new LinearGradientBrush(band, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)],
                        Positions = [0f, 0.5f, 1f]
                    }
                };
                g.FillRectangle(shineBrush, band);
                g.ResetClip();
            }

            // Tıklama dalgaları
            if (_ripples.Count > 0)
            {
                g.SetClip(path);
                foreach (var ripple in _ripples)
                {
                    var t = ripple.Age / 0.7f;
                    var r = (float)Easing.OutCubic(t) * body.Width;
                    using var brush = new SolidBrush(Color.FromArgb((int)((1 - t) * 60), 255, 255, 255));
                    g.FillEllipse(brush, ripple.Origin.X - r, ripple.Origin.Y - r, r * 2, r * 2);
                }
                g.ResetClip();
            }

            var textColor = Ghost ? Theme.Lerp(Theme.TextPrimary, Color.White, hover) : Color.White;
            var contentAlpha = (1f - _loading.Value) * enabledAlpha;

            if (contentAlpha > 0.01f)
            {
                var font = Theme.SemiBold(14.5f);
                var iconFont = Theme.Icon(14f);
                var textSize = g.MeasureString(Text, font);
                var iconWidth = string.IsNullOrEmpty(Glyph) ? 0 : 24f;
                var total = textSize.Width + iconWidth;
                var x = body.X + (body.Width - total) / 2f;
                var cy = body.Y + body.Height / 2f;
                var lift = _loading.Value * -6f;

                using var brush = new SolidBrush(Theme.Alpha(textColor, contentAlpha));
                if (iconWidth > 0)
                {
                    g.DrawString(Glyph, iconFont, brush, new RectangleF(x, cy - 12 + lift, 20, 24), Theme.Center);
                }
                g.DrawString(Text, font, brush, new RectangleF(x + iconWidth, cy - textSize.Height / 2f + lift, textSize.Width + 4, textSize.Height), Theme.TopLeft);
            }

            if (_loading.Value > 0.01f)
            {
                var d = 20f;
                var cx = body.X + body.Width / 2f;
                var cy = body.Y + body.Height / 2f;
                var rect = new RectangleF(cx - d / 2, cy - d / 2, d, d);
                using var track = new Pen(Theme.Alpha(textColor, 0.25f * _loading.Value), 2.4f);
                g.DrawEllipse(track, rect);
                using var arc = new Pen(Theme.Alpha(textColor, _loading.Value), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var sweep = 90f + 120f * (float)Math.Sin(FrameClock.Time * 3) * 0.5f + 60f;
                g.DrawArc(arc, rect, _spin, sweep);
            }

            if (Focused && ShowFocusCues)
            {
                using var focusPen = new Pen(Theme.Alpha(Color.White, 0.5f), 1f) { DashStyle = DashStyle.Dot };
                using var focusPath = Theme.RoundRect(RectangleF.Inflate(body, -3, -3), Radius - 3);
                g.DrawPath(focusPen, focusPath);
            }
        }

        private sealed class Ripple(PointF origin)
        {
            public PointF Origin { get; } = origin;
            public float Age { get; set; }
        }
    }
}
