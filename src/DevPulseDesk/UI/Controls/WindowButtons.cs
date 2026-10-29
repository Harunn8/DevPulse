using System.ComponentModel;
using DevPulseDesk.Forms;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>Kenarlıksız pencereler için küçült / büyüt / kapat butonları.</summary>
    internal sealed class WindowButtons : CanvasControl
    {
        private const float ButtonWidth = 46f;
        private readonly SmoothValue[] _hover = [new(0, 16), new(0, 16), new(0, 16)];
        private int _hoverIndex = -1;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowMaximize { get; set; } = true;

        public int PreferredWidth => Dpi.S(ButtonWidth * (ShowMaximize ? 3 : 2));

        private string[] Buttons
        {
            get
            {
                var maximized = FindForm()?.WindowState == FormWindowState.Maximized;
                return ShowMaximize
                    ? [Glyph.Minimize, maximized ? Glyph.Restore : Glyph.Maximize, Glyph.Close]
                    : [Glyph.Minimize, Glyph.Close];
            }
        }

        private int HitTest(Point p)
        {
            var x = ToLogical(p).X;
            var index = (int)(x / ButtonWidth);
            return index >= 0 && index < Buttons.Length ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHover(HitTest(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(-1);
        }

        private void SetHover(int index)
        {
            if (index == _hoverIndex) return;
            _hoverIndex = index;
            for (var i = 0; i < _hover.Length; i++) _hover[i].Target = i == index ? 1 : 0;
            StartTicking();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            var form = FindForm();
            if (form == null) return;

            var glyph = HitTest(e.Location) is var i and >= 0 ? Buttons[i] : null;
            switch (glyph)
            {
                case Glyph.Minimize:
                    form.WindowState = FormWindowState.Minimized;
                    break;
                case Glyph.Maximize or Glyph.Restore:
                    (form as BorderlessForm)?.ToggleMaximize();
                    break;
                case Glyph.Close:
                    if (form is BorderlessForm borderless) borderless.CloseAnimated();
                    else form.Close();
                    break;
            }
        }

        protected override bool OnFrame(float dt) => _hover.Aggregate(false, (moving, h) => h.Step(dt) | moving);

        protected override void OnRender(Graphics g, SizeF size)
        {
            var buttons = Buttons;
            for (var i = 0; i < buttons.Length; i++)
            {
                var rect = new RectangleF(i * ButtonWidth, 0, ButtonWidth, size.Height);
                var isClose = buttons[i] == Glyph.Close;
                var hover = _hover[i].Value;
                if (hover > 0.01f)
                {
                    var color = isClose ? Color.FromArgb(232, 17, 35) : Theme.SurfaceHover;
                    using var bg = new SolidBrush(Theme.Alpha(color, hover));
                    g.FillRectangle(bg, rect);
                }

                using var brush = new SolidBrush(isClose ? Theme.Lerp(Theme.TextSecondary, Color.White, hover) : Theme.Lerp(Theme.TextSecondary, Theme.TextPrimary, hover));
                g.DrawString(buttons[i], Theme.Icon(10f), brush, rect, Theme.Center);
            }
        }
    }
}
