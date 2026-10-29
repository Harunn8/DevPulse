using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    internal sealed class ToggleSwitch : CanvasControl
    {
        private readonly SmoothValue _knob = new(0, 16);
        private readonly SmoothValue _hover = new(0, 14);
        private bool _checked;

        public ToggleSwitch()
        {
            Cursor = Cursors.Hand;
            BackColor = Theme.Chrome;
        }

        public event EventHandler? CheckedChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                _knob.Target = value ? 1 : 0;
                StartTicking();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetCheckedInstant(bool value)
        {
            _checked = value;
            _knob.Snap(value ? 1 : 0);
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
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

        protected override bool OnFrame(float dt) => _knob.Step(dt) | _hover.Step(dt);

        protected override void OnRender(Graphics g, SizeF size)
        {
            var k = _knob.Value;
            var track = new RectangleF(1, (size.Height - 22) / 2f, 40, 22);
            using var path = Theme.RoundRect(track, 11);

            using (var off = new SolidBrush(Theme.Lerp(Theme.SurfaceAlt, Theme.SurfaceHover, _hover.Value)))
                g.FillPath(off, path);

            if (k > 0.01f)
            {
                using var on = new LinearGradientBrush(track, Theme.Alpha(Theme.Accent, k), Theme.Alpha(Theme.Accent2, k), 0f);
                g.FillPath(on, path);
            }

            using (var border = new Pen(Theme.Alpha(Theme.Border, 1 - k), 1f)) g.DrawPath(border, path);

            var knobX = track.X + 3 + k * (track.Width - 22);
            var knobRect = new RectangleF(knobX, track.Y + 3, 16, 16);
            if (k > 0.5f) Theme.RadialLight(g, new PointF(knobRect.X + 8, knobRect.Y + 8), 16, Theme.Accent, (int)(90 * k));
            using (var knob = new SolidBrush(Color.White)) g.FillEllipse(knob, knobRect);

            using var textBrush = new SolidBrush(Theme.Lerp(Theme.TextSecondary, Theme.TextPrimary, _hover.Value));
            g.DrawString(Text, Theme.Font(13.5f), textBrush, new RectangleF(track.Right + 10, 0, size.Width - track.Right - 10, size.Height), Theme.LeftCenter);
        }
    }
}
