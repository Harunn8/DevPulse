using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    public enum DeviceFilter { All, Running, Success, Fail, Pending }

    /// <summary>
    /// Sol menü: kayan seçim göstergeli filtreler, canlı nabız widget'ı ve kullanıcı kartı.
    /// </summary>
    internal sealed class SideNav : CanvasControl
    {
        private const float ItemTop = 54f;
        private const float ItemHeight = 44f;
        private const float ItemGap = 4f;

        private readonly NavItem[] _items =
        [
            new(DeviceFilter.All, Glyph.Devices, "Tüm Cihazlar", Theme.Accent),
            new(DeviceFilter.Running, Glyph.Sync, "Test Ediliyor", Theme.Accent2),
            new(DeviceFilter.Success, Glyph.Check, "Başarılı", Theme.Success),
            new(DeviceFilter.Fail, Glyph.Cancel, "Başarısız", Theme.Danger),
            new(DeviceFilter.Pending, Glyph.Clock, "Bekleyen", Theme.TextSecondary)
        ];

        private readonly SmoothValue _indicator = new(0, 14);
        private readonly SmoothValue _logoutHover = new(0, 14);
        private int _selected;
        private float _time;
        private bool _layoutMoving;

        public SideNav()
        {
            BackColor = Theme.Chrome;
            Cursor = Cursors.Default;
        }

        protected override bool AlwaysAnimate => true;

        public event EventHandler<DeviceFilter>? FilterChanged;
        public event EventHandler? LogoutRequested;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string UserName { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDemo { get; set; }

        /// <summary>Son test bilgisi (nabız widget'ında gösterilir).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PulseCaption { get; set; } = "Henüz test çalıştırılmadı";

        /// <summary>Test sürerken nabız hızlanır.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsBusy { get; set; }

        public void UpdateCounts(int all, int running, int success, int fail, int pending)
        {
            int[] counts = [all, running, success, fail, pending];
            for (var i = 0; i < _items.Length; i++) _items[i].Count = counts[i];
            Invalidate();
        }

        public void Select(DeviceFilter filter)
        {
            var index = Array.FindIndex(_items, i => i.Filter == filter);
            if (index < 0 || index == _selected) return;
            _selected = index;
            _indicator.Target = index;
            StartTicking();
            FilterChanged?.Invoke(this, filter);
        }

        private RectangleF ItemRect(int index, SizeF size) =>
            new(14, ItemTop + index * (ItemHeight + ItemGap), size.Width - 28, ItemHeight);

        private RectangleF PulseCardRect(SizeF size) =>
            new(14, ItemTop + _items.Length * (ItemHeight + ItemGap) + 26, size.Width - 28, 112);

        private RectangleF UserCardRect(SizeF size) => new(14, size.Height - 82, size.Width - 28, 66);

        private RectangleF LogoutRect(SizeF size)
        {
            var card = UserCardRect(size);
            return new RectangleF(card.Right - 44, card.Y + (card.Height - 34) / 2f, 34, 34);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = ToLogical(e.Location);
            var size = Logical;
            var hoverIndex = -1;
            for (var i = 0; i < _items.Length; i++)
            {
                if (ItemRect(i, size).Contains(p)) hoverIndex = i;
                _items[i].Hover.Target = i == hoverIndex ? 1 : 0;
            }
            var overLogout = LogoutRect(size).Contains(p);
            _logoutHover.Target = overLogout ? 1 : 0;
            Cursor = hoverIndex >= 0 || overLogout ? Cursors.Hand : Cursors.Default;
            StartTicking();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            foreach (var item in _items) item.Hover.Target = 0;
            _logoutHover.Target = 0;
            StartTicking();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var p = ToLogical(e.Location);
            var size = Logical;
            if (LogoutRect(size).Contains(p))
            {
                LogoutRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            for (var i = 0; i < _items.Length; i++)
            {
                if (ItemRect(i, size).Contains(p)) Select(_items[i].Filter);
            }
        }

        protected override bool OnFrame(float dt)
        {
            _time += dt * (IsBusy ? 1.8f : 1f);
            var moving = _indicator.Step(dt) | _logoutHover.Step(dt);
            foreach (var item in _items) moving |= item.Hover.Step(dt);
            _layoutMoving = moving;
            return moving;
        }

        protected override void InvalidateFrame()
        {
            if (_layoutMoving)
            {
                Invalidate();
                return;
            }

            // Boştayken yalnızca nabız widget'ını yeniden çiz.
            var r = PulseCardRect(Logical);
            Invalidate(new Rectangle((int)(r.X * DpiScale) - 2, (int)(r.Y * DpiScale) - 2, (int)(r.Width * DpiScale) + 4, (int)(r.Height * DpiScale) + 4));
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            using (var line = new Pen(Theme.Border, 1f)) g.DrawLine(line, size.Width - 0.5f, 0, size.Width - 0.5f, size.Height);

            using (var section = new SolidBrush(Theme.TextMuted))
                g.DrawString("CİHAZ FİLTRELERİ", Theme.Font(11f, FontStyle.Bold), section, 24, 26);

            // Kayan seçim göstergesi
            var indicatorRect = ItemRect(0, size);
            indicatorRect.Y += _indicator.Value * (ItemHeight + ItemGap);
            using (var path = Theme.RoundRect(indicatorRect, 10))
            {
                using var fill = new LinearGradientBrush(indicatorRect, Theme.Alpha(Theme.Accent, 38), Theme.Alpha(Theme.Accent2, 14), 0f);
                g.FillPath(fill, path);
                using var border = new Pen(Theme.Alpha(Theme.Accent, 50), 1f);
                g.DrawPath(border, path);
            }
            var bar = new RectangleF(indicatorRect.X - 1, indicatorRect.Y + 11, 3.5f, indicatorRect.Height - 22);
            using (var barPath = Theme.RoundRect(bar, 1.75f))
            using (var barBrush = new LinearGradientBrush(RectangleF.Inflate(bar, 0, 1), Theme.Accent, Theme.Accent2, 90f))
            {
                g.FillPath(barBrush, barPath);
            }

            for (var i = 0; i < _items.Length; i++) DrawItem(g, _items[i], ItemRect(i, size), i);

            DrawPulseCard(g, PulseCardRect(size));
            DrawUserCard(g, size);
        }

        private void DrawItem(Graphics g, NavItem item, RectangleF rect, int index)
        {
            var selected = Math.Clamp(1f - Math.Abs(_indicator.Value - index), 0f, 1f);
            var hover = item.Hover.Value * (1 - selected);
            if (hover > 0.01f)
            {
                using var path = Theme.RoundRect(rect, 10);
                using var fill = new SolidBrush(Theme.Alpha(Theme.SurfaceHover, hover * 0.8f));
                g.FillPath(fill, path);
            }

            var iconColor = Theme.Lerp(Theme.TextMuted, item.Color, Math.Max(selected, hover * 0.6f));
            using (var icon = new SolidBrush(iconColor))
                g.DrawString(item.Glyph, Theme.Icon(15f), icon, new RectangleF(rect.X + 10, rect.Y, 28, rect.Height), Theme.Center);

            using (var text = new SolidBrush(Theme.Lerp(Theme.TextSecondary, Theme.TextPrimary, Math.Max(selected, hover))))
                g.DrawString(item.Title, selected > 0.5f ? Theme.SemiBold(14f) : Theme.Font(14f), text, new RectangleF(rect.X + 44, rect.Y, rect.Width - 90, rect.Height), Theme.LeftCenter);

            var countText = item.Count.ToString();
            var font = Theme.SemiBold(11.5f);
            var w = Math.Max(24, g.MeasureString(countText, font).Width + 10);
            var pill = new RectangleF(rect.Right - w - 10, rect.Y + (rect.Height - 20) / 2f, w, 20);
            using (var path = Theme.RoundRect(pill, 10))
            using (var fill = new SolidBrush(Theme.Alpha(item.Color, (int)(22 + 30 * selected))))
            {
                g.FillPath(fill, path);
            }
            using (var countBrush = new SolidBrush(Theme.Lerp(Theme.TextSecondary, item.Color, 0.5f + selected * 0.5f)))
                g.DrawString(countText, font, countBrush, pill, Theme.Center);
        }

        private void DrawPulseCard(Graphics g, RectangleF card)
        {
            using (var bg = new SolidBrush(Theme.Chrome)) g.FillRectangle(bg, RectangleF.Inflate(card, 2, 2));
            using (var path = Theme.RoundRect(card, 12))
            {
                using var fill = new LinearGradientBrush(card, Theme.Surface, Theme.Chrome, 90f);
                g.FillPath(fill, path);
                using var border = new Pen(Theme.Border, 1f);
                g.DrawPath(border, path);
            }

            var beat = 0.5f + 0.5f * MathF.Sin(_time * 6f);
            var color = IsBusy ? Theme.Accent2 : Theme.Success;
            Theme.RadialLight(g, new PointF(card.X + 18, card.Y + 20), 10, color, (int)(150 * beat));
            using (var dot = new SolidBrush(color)) g.FillEllipse(dot, card.X + 14, card.Y + 16, 8, 8);
            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString(IsBusy ? "Test sürüyor" : "Sistem nabzı", Theme.SemiBold(13f), title, card.X + 28, card.Y + 10);

            var trace = new RectangleF(card.X + 8, card.Y + 34, card.Width - 16, 44);
            var state = g.Save();
            g.SetClip(trace);
            var speed = IsBusy ? 150f : 90f;
            var head = trace.X + (_time * speed % (trace.Width + 120));
            Pulse.DrawTrace(g, trace, head, IsBusy ? 70f : 110f, 150f, color, 1.8f);
            g.Restore(state);

            using var caption = new SolidBrush(Theme.TextMuted);
            g.DrawString(PulseCaption, Theme.Font(11.5f), caption, new RectangleF(card.X + 14, card.Bottom - 28, card.Width - 24, 20), Theme.LeftCenter);
        }

        private void DrawUserCard(Graphics g, SizeF size)
        {
            var card = UserCardRect(size);
            using (var path = Theme.RoundRect(card, 14))
            {
                using var fill = new SolidBrush(Theme.Surface);
                g.FillPath(fill, path);
                using var border = new Pen(Theme.Border, 1f);
                g.DrawPath(border, path);
            }

            var avatar = new RectangleF(card.X + 12, card.Y + 13, 40, 40);
            using (var fill = new LinearGradientBrush(avatar, Theme.Accent, Theme.Accent2, 45f)) g.FillEllipse(fill, avatar);
            using (var white = new SolidBrush(Color.White)) g.DrawString(Theme.Initials(UserName), Theme.SemiBold(14f), white, avatar, Theme.Center);

            var statusColor = IsDemo ? Theme.Warning : Theme.Success;
            using (var ring = new SolidBrush(Theme.Surface)) g.FillEllipse(ring, avatar.Right - 12, avatar.Bottom - 12, 14, 14);
            using (var status = new SolidBrush(statusColor)) g.FillEllipse(status, avatar.Right - 10, avatar.Bottom - 10, 10, 10);

            using (var name = new SolidBrush(Theme.TextPrimary))
                g.DrawString(UserName, Theme.SemiBold(13.5f), name, new RectangleF(avatar.Right + 10, card.Y + 13, card.Width - 112, 20), Theme.LeftCenter);
            using (var sub = new SolidBrush(Theme.TextMuted))
                g.DrawString(IsDemo ? "Demo oturumu" : "Çevrimiçi", Theme.Font(12f), sub, new RectangleF(avatar.Right + 10, card.Y + 33, card.Width - 112, 20), Theme.LeftCenter);

            var logout = LogoutRect(size);
            var hover = _logoutHover.Value;
            if (hover > 0.01f)
            {
                using var path = Theme.RoundRect(logout, 9);
                using var fill = new SolidBrush(Theme.Alpha(Theme.Danger, hover * 0.18f));
                g.FillPath(fill, path);
            }
            using var icon = new SolidBrush(Theme.Lerp(Theme.TextMuted, Theme.Danger, hover));
            g.DrawString(Glyph.Power, Theme.Icon(14f), icon, logout, Theme.Center);
        }

        private sealed class NavItem(DeviceFilter filter, string glyph, string title, Color color)
        {
            public DeviceFilter Filter { get; } = filter;
            public string Glyph { get; } = glyph;
            public string Title { get; } = title;
            public Color Color { get; } = color;
            public int Count { get; set; }
            public SmoothValue Hover { get; } = new(0, 14);
        }
    }
}
