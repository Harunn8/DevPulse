using System.Drawing.Drawing2D;
using DevPulseDesk.Services;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>Sağ üstten kayarak gelen, süresi dolunca kendiliğinden kapanan bildirim.</summary>
    internal sealed class Toast : CanvasControl
    {
        private const float DisplaySeconds = 4.2f;
        private string _title = "";
        private string _message = "";
        private LogLevel _level;
        private float _life;
        private Tween? _slide;
        private bool _leaving;

        public Toast()
        {
            BackColor = Theme.SurfaceAlt;
            Visible = false;
            Cursor = Cursors.Hand;
            Size = Dpi.S(360, 84);
        }

        public void Notify(LogLevel level, string title, string message)
        {
            _level = level;
            _title = title;
            _message = message;
            _life = 0;
            _leaving = false;

            var parent = Parent;
            if (parent == null) return;
            var finalLeft = parent.ClientSize.Width - Width - Dpi.S(28);
            Top = Dpi.S(62);
            if (!Visible) Left = parent.ClientSize.Width + Dpi.S(10);
            var startLeft = Left;
            Visible = true;
            BringToFront();

            _slide?.Cancel();
            _slide = Animator.Animate(520, Easing.OutBack, t => Left = startLeft + (int)((finalLeft - startLeft) * t));
            StartTicking();
            Invalidate();
        }

        private void Dismiss()
        {
            if (_leaving || Parent == null) return;
            _leaving = true;
            var startLeft = Left;
            var endLeft = Parent.ClientSize.Width + Dpi.S(10);
            _slide?.Cancel();
            _slide = Animator.Animate(320, Easing.InCubic, t => Left = startLeft + (int)((endLeft - startLeft) * t), () => Visible = false);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Dismiss();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using var path = Theme.RoundRect(new RectangleF(0, 0, Width, Height), 14 * DpiScale);
            Region = new Region(path);
        }

        protected override bool OnFrame(float dt)
        {
            if (!Visible) return false;
            if (!ClientRectangle.Contains(PointToClient(MousePosition))) _life += dt; // fare üzerindeyken bekle
            if (_life >= DisplaySeconds) Dismiss();
            return !_leaving;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var color = _level switch
            {
                LogLevel.Success => Theme.Success,
                LogLevel.Error => Theme.Danger,
                LogLevel.Warning => Theme.Warning,
                LogLevel.System => Theme.Accent2,
                _ => Theme.Accent
            };
            var glyph = _level switch
            {
                LogLevel.Success => Glyph.Check,
                LogLevel.Error => Glyph.Cancel,
                LogLevel.Warning => Glyph.Warning,
                _ => Glyph.Info
            };

            var rect = new RectangleF(0, 0, size.Width, size.Height);
            using (var fill = new LinearGradientBrush(rect, Theme.SurfaceAlt, Theme.Surface, 90f)) g.FillRectangle(fill, rect);
            Theme.RadialLight(g, new PointF(34, size.Height / 2f), 70, color, 50);
            using (var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, size.Width - 1, size.Height - 1), 14))
            using (var border = new Pen(Theme.Alpha(color, 90), 1f))
            {
                g.DrawPath(border, path);
            }

            var icon = new RectangleF(16, size.Height / 2f - 18, 36, 36);
            using (var iconFill = new SolidBrush(Theme.Alpha(color, 40))) g.FillEllipse(iconFill, icon);
            using (var iconBrush = new SolidBrush(color)) g.DrawString(glyph, Theme.Icon(15f), iconBrush, icon, Theme.Center);

            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString(_title, Theme.SemiBold(14f), title, new RectangleF(64, 16, size.Width - 80, 20), Theme.LeftCenter);
            using (var message = new SolidBrush(Theme.TextSecondary))
                g.DrawString(_message, Theme.Font(12.5f), message, new RectangleF(64, 37, size.Width - 80, 36), new StringFormat { Trimming = StringTrimming.EllipsisWord });

            // Kalan süre çubuğu
            var remaining = 1f - Math.Clamp(_life / DisplaySeconds, 0, 1);
            var bar = new RectangleF(0, size.Height - 3, size.Width * remaining, 3);
            using var barBrush = new LinearGradientBrush(new RectangleF(0, size.Height - 3, size.Width, 3), color, Theme.Accent2, 0f);
            g.FillRectangle(barBrush, bar);
        }
    }
}
