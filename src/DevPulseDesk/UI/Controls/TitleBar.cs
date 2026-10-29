using System.ComponentModel;
using DevPulseDesk.Forms;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>Ana pencerenin özel başlık çubuğu: logo, sayfa adı, sunucu rozeti ve pencere butonları.</summary>
    internal sealed class TitleBar : CanvasControl
    {
        private readonly WindowButtons _buttons;

        public TitleBar()
        {
            BackColor = Theme.Chrome;
            _buttons = new WindowButtons { Dock = DockStyle.Right, BackColor = Theme.Chrome };
            Controls.Add(_buttons);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PageTitle { get; set; } = "Test Merkezi";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Badge { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDemo { get; set; }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _buttons.Width = _buttons.PreferredWidth;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 2) (FindForm() as BorderlessForm)?.ToggleMaximize();
            else Native.DragWindow(FindForm());
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            using (var line = new Pen(Theme.Border, 1f)) g.DrawLine(line, 0, size.Height - 0.5f, size.Width, size.Height - 0.5f);

            var cy = size.Height / 2f;
            Pulse.DrawLogo(g, new RectangleF(16, cy - 11, 22, 22), 0);

            using var primary = new SolidBrush(Theme.TextPrimary);
            using var secondary = new SolidBrush(Theme.TextSecondary);
            var brandFont = Theme.SemiBold(14f);
            g.DrawString("DevPulse", brandFont, primary, new RectangleF(46, 0, 100, size.Height), Theme.LeftCenter);
            var brandWidth = g.MeasureString("DevPulse", brandFont).Width;

            var x = 46 + brandWidth + 4;
            using (var muted = new SolidBrush(Theme.TextMuted))
                g.DrawString("/", Theme.Font(14f), muted, new RectangleF(x, 0, 16, size.Height), Theme.Center);
            x += 18;
            g.DrawString(PageTitle, Theme.Font(13.5f), secondary, new RectangleF(x, 0, 160, size.Height), Theme.LeftCenter);
            x += g.MeasureString(PageTitle, Theme.Font(13.5f)).Width + 12;

            if (!string.IsNullOrEmpty(Badge))
            {
                var color = IsDemo ? Theme.Warning : Theme.Success;
                var font = Theme.Font(11.5f);
                var textWidth = Math.Min(320, g.MeasureString(Badge, font).Width);
                var pill = new RectangleF(x, cy - 11, textWidth + 28, 22);
                using var path = Theme.RoundRect(pill, 11);
                using (var fill = new SolidBrush(Theme.Alpha(color, 26))) g.FillPath(fill, path);
                using (var border = new Pen(Theme.Alpha(color, 70), 1f)) g.DrawPath(border, path);
                using (var dot = new SolidBrush(color)) g.FillEllipse(dot, pill.X + 9, cy - 3, 6, 6);
                using var text = new SolidBrush(Theme.Lerp(color, Color.White, 0.3f));
                g.DrawString(Badge, font, text, new RectangleF(pill.X + 19, pill.Y, textWidth + 4, pill.Height), Theme.LeftCenter);
            }
        }
    }
}
