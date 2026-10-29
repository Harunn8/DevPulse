using System.ComponentModel;
using DevPulseDesk.UI;
using DevPulseDesk.UI.Controls;

namespace DevPulseDesk.Forms
{
    /// <summary>
    /// Kenarlıksız, yuvarlak köşeli, açılış/kapanışta solarak geçiş yapan modern pencere tabanı.
    /// </summary>
    internal class BorderlessForm : Form
    {
        private bool _closingAnimated;

        public BorderlessForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Background;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Native(13);
            Opacity = 0;
        }

        /// <summary>Kenarlardan yeniden boyutlandırma (form Padding'i kavrama alanı olur).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Resizable { get; set; }

        protected int ResizeBorder => Dpi.S(5);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= Native.WS_MINIMIZEBOX; // görev çubuğundan küçült/geri yükle
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyModernWindowStyle(Handle);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await Effects.FadeAsync(this, 0, 1, 380);
        }

        public async void CloseAnimated()
        {
            if (_closingAnimated) return;
            _closingAnimated = true;
            await Effects.FadeAsync(this, Opacity, 0, 220);
            Close();
        }

        public void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
            else
            {
                var screen = Screen.FromHandle(Handle);
                var area = screen.WorkingArea;
                MaximizedBounds = new Rectangle(area.X - screen.Bounds.X, area.Y - screen.Bounds.Y, area.Width, area.Height);
                WindowState = FormWindowState.Maximized;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Resizable)
            {
                var pad = WindowState == FormWindowState.Maximized ? 0 : ResizeBorder;
                if (Padding.All != pad) Padding = new Padding(pad);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != Native.WM_NCHITTEST || !Resizable || WindowState != FormWindowState.Normal) return;

            var pos = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
            var grip = ResizeBorder;
            var left = pos.X <= grip;
            var right = pos.X >= ClientSize.Width - grip;
            var top = pos.Y <= grip;
            var bottom = pos.Y >= ClientSize.Height - grip;

            var hit = (top, bottom, left, right) switch
            {
                (true, _, true, _) => 13,
                (true, _, _, true) => 14,
                (_, true, true, _) => 16,
                (_, true, _, true) => 17,
                (true, _, _, _) => 12,
                (_, true, _, _) => 15,
                (_, _, true, _) => 10,
                (_, _, _, true) => 11,
                _ => 0
            };
            if (hit != 0) m.Result = hit;
        }
    }
}
