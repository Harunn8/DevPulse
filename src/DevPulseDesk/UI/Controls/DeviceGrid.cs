using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.Services;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Duyarlı (responsive) cihaz kartı ızgarası. Kartlar filtrelemede yeni konumlarına akar,
    /// test sırasında parıltı/ışık süzmesi gösterir, sonuçta rozet "pop" animasyonuyla belirir.
    /// Kendi yumuşak kaydırması ve ince kaydırma çubuğu vardır.
    /// </summary>
    internal sealed class DeviceGrid : CanvasControl
    {
        private const float CardHeight = 152f;
        private const float Gap = 16f;
        private const float MinCardWidth = 300f;
        private const float ScrollbarSpace = 10f;

        private readonly Dictionary<DeviceTestItem, CardVisual> _visuals = new();
        private readonly SmoothValue _scroll = new(0, 12);
        private readonly SmoothValue _scrollbarHover = new(0, 12);
        private readonly SmoothValue _loadingFade = new(0, 8);
        private IReadOnlyList<DeviceTestItem> _items = Array.Empty<DeviceTestItem>();
        private List<DeviceTestItem> _visible = new();
        private DeviceFilter _filter = DeviceFilter.All;
        private string _search = "";
        private float _cardWidth = MinCardWidth;
        private float _contentHeight;
        private float _time;
        private bool _isLoading;
        private CardVisual? _hovered;
        private float? _dragOffset;

        public DeviceGrid()
        {
            BackColor = Theme.Background;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                _loadingFade.Target = value ? 1 : 0;
                StartTicking();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DeviceFilter Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                Relayout(true);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SearchText
        {
            get => _search;
            set
            {
                _search = value?.Trim() ?? "";
                Relayout(true);
            }
        }

        public int VisibleCount => _visible.Count;

        public void SetItems(IReadOnlyList<DeviceTestItem> items)
        {
            _items = items;
            _visuals.Clear();
            foreach (var item in items) _visuals[item] = new CardVisual(item.State);
            _scroll.Snap(0);
            Relayout(true);
        }

        /// <summary>Bir cihazın durumu değiştiğinde çağrılır; geçiş animasyonlarını tetikler.</summary>
        public void NotifyChanged(DeviceTestItem item)
        {
            if (_visuals.TryGetValue(item, out var visual) && visual.LastState != item.State)
            {
                visual.LastState = item.State;
                visual.Pop.Snap(0);
                visual.Pop.Target = 1;
                if (item.IsDone) visual.Flash = 1f;
            }

            // Filtre durum bazlıysa kart görünürlüğü değişebilir.
            if (_filter != DeviceFilter.All) Relayout(false);
            StartTicking();
        }

        public void NotifyAllChanged()
        {
            foreach (var item in _items) NotifyChanged(item);
            Relayout(false);
        }

        private bool Matches(DeviceTestItem item)
        {
            var filterOk = _filter switch
            {
                DeviceFilter.Running => item.State == TestState.Running,
                DeviceFilter.Success => item.State == TestState.Success,
                DeviceFilter.Fail => item.State == TestState.Fail,
                DeviceFilter.Pending => item.State is TestState.Pending or TestState.Cancelled,
                _ => true
            };
            if (!filterOk) return false;
            if (_search.Length == 0) return true;
            return item.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase)
                   || item.Host.Contains(_search, StringComparison.OrdinalIgnoreCase)
                   || item.Subtitle.Contains(_search, StringComparison.CurrentCultureIgnoreCase);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout(false, snap: true);
        }

        private void Relayout(bool stagger, bool snap = false)
        {
            var size = Logical;
            if (size.Width < 50) return;

            var available = size.Width - ScrollbarSpace;
            var columns = Math.Max(1, (int)((available + Gap) / (MinCardWidth + Gap)));
            _cardWidth = (available - (columns - 1) * Gap) / columns;

            var newVisible = _items.Where(Matches).ToList();
            var order = 0;
            foreach (var item in newVisible)
            {
                var visual = _visuals[item];
                var index = order++;
                var x = index % columns * (_cardWidth + Gap);
                var y = index / columns * (CardHeight + Gap) + 4;

                if (!visual.Shown || snap)
                {
                    visual.X.Snap(x);
                    visual.Y.Snap(snap && visual.Shown ? y : y + 26);
                    if (!visual.Shown)
                    {
                        visual.Alpha.Snap(0);
                        visual.Delay = stagger ? Math.Min(index, 14) * 0.04f : 0f;
                    }
                }
                visual.X.Target = x;
                visual.Y.Target = y;
                visual.Alpha.Target = 1;
                visual.Shown = true;
            }

            foreach (var item in _items.Except(newVisible))
            {
                var visual = _visuals[item];
                visual.Shown = false;
                visual.Alpha.Target = 0;
            }

            _visible = newVisible;
            var rows = (int)Math.Ceiling(_visible.Count / (float)columns);
            _contentHeight = rows == 0 ? 0 : rows * (CardHeight + Gap) - Gap + 8;
            _scroll.Target = Math.Clamp(_scroll.Target, 0, MaxScroll);
            StartTicking();
        }

        private float MaxScroll => Math.Max(0, _contentHeight - Logical.Height);

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scroll.Target = Math.Clamp(_scroll.Target - e.Delta / 120f * 90f, 0, MaxScroll);
            StartTicking();
        }

        private RectangleF ThumbRect(SizeF size)
        {
            if (_contentHeight <= size.Height) return RectangleF.Empty;
            var thumbH = Math.Max(40, size.Height * size.Height / _contentHeight);
            var y = MaxScroll <= 0 ? 0 : _scroll.Value / MaxScroll * (size.Height - thumbH);
            return new RectangleF(size.Width - 7, y, 6, thumbH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var p = ToLogical(e.Location);
            var thumb = ThumbRect(Logical);
            if (!thumb.IsEmpty && p.X >= thumb.X - 6)
            {
                _dragOffset = thumb.Contains(p) ? p.Y - thumb.Y : thumb.Height / 2f;
                DragTo(p.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragOffset = null;
        }

        private void DragTo(float mouseY)
        {
            var size = Logical;
            var thumb = ThumbRect(size);
            if (thumb.IsEmpty || _dragOffset == null) return;
            var track = size.Height - thumb.Height;
            var ratio = track <= 0 ? 0 : Math.Clamp((mouseY - _dragOffset.Value) / track, 0, 1);
            _scroll.Snap(ratio * MaxScroll);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = ToLogical(e.Location);
            if (_dragOffset != null)
            {
                DragTo(p.Y);
                return;
            }

            var size = Logical;
            _scrollbarHover.Target = p.X > size.Width - 14 && _contentHeight > size.Height ? 1 : 0;

            var content = new PointF(p.X, p.Y + _scroll.Value);
            CardVisual? hovered = null;
            foreach (var item in _visible)
            {
                var v = _visuals[item];
                if (new RectangleF(v.X.Value, v.Y.Value, _cardWidth, CardHeight).Contains(content)) hovered = v;
            }
            if (hovered != _hovered)
            {
                if (_hovered != null) _hovered.Hover.Target = 0;
                if (hovered != null) hovered.Hover.Target = 1;
                _hovered = hovered;
            }
            StartTicking();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hovered != null) _hovered.Hover.Target = 0;
            _hovered = null;
            _scrollbarHover.Target = 0;
            StartTicking();
        }

        protected override bool OnFrame(float dt)
        {
            _time += dt;
            var moving = _scroll.Step(dt) | _scrollbarHover.Step(dt) | _loadingFade.Step(dt);
            foreach (var (item, v) in _visuals)
            {
                if (v.Delay > 0)
                {
                    v.Delay -= dt;
                    moving = true;
                    continue;
                }
                moving |= v.X.Step(dt) | v.Y.Step(dt) | v.Alpha.Step(dt) | v.Hover.Step(dt) | v.Pop.Step(dt);
                if (v.Flash > 0)
                {
                    v.Flash = Math.Max(0, v.Flash - dt * 1.2f);
                    moving = true;
                }
                if (item.State == TestState.Running && v.Alpha.Value > 0.01f) moving = true;
            }
            return moving || _isLoading || _loadingFade.Value > 0.01f;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            if (_loadingFade.Value > 0.01f) DrawSkeleton(g, size, _loadingFade.Value);
            if (_isLoading) return;

            if (_visible.Count == 0 && _visuals.Values.All(v => v.Alpha.Value < 0.02f))
            {
                DrawEmpty(g, size);
                return;
            }

            var state = g.Save();
            g.TranslateTransform(0, -_scroll.Value);
            foreach (var item in _items)
            {
                var v = _visuals[item];
                if (v.Alpha.Value < 0.01f || v.Delay > 0) continue;
                var top = v.Y.Value - _scroll.Value;
                if (top > size.Height + 10 || top + CardHeight < -10) continue;
                DrawCard(g, item, v);
            }
            g.Restore(state);

            DrawScrollbar(g, size);
        }

        private static (Color Color, string Label, string Glyph) StyleOf(TestState state) => state switch
        {
            TestState.Running => (Theme.Accent, "TEST EDİLİYOR", Glyph.Sync),
            TestState.Success => (Theme.Success, "BAŞARILI", Glyph.Check),
            TestState.Fail => (Theme.Danger, "BAŞARISIZ", Glyph.Cancel),
            TestState.Cancelled => (Theme.Warning, "İPTAL", Glyph.Warning),
            _ => (Theme.TextMuted, "BEKLİYOR", Glyph.Clock)
        };

        private void DrawCard(Graphics g, DeviceTestItem item, CardVisual v)
        {
            var a = v.Alpha.Value;
            Color C(Color c) => Theme.Alpha(c, (int)(c.A * a));

            var hover = v.Hover.Value;
            var rect = new RectangleF(v.X.Value, v.Y.Value - hover * 3f, _cardWidth, CardHeight);
            var (color, label, glyph) = StyleOf(item.State);
            var running = item.State == TestState.Running;
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * 4f);

            // Gölge ve parıltı
            using (var shadowPath = Theme.RoundRect(new RectangleF(rect.X + 3, rect.Y + 7, rect.Width - 6, rect.Height), 16))
            using (var shadow = new SolidBrush(Color.FromArgb((int)(55 * a * (0.4f + hover)), 0, 0, 0)))
            {
                g.FillPath(shadow, shadowPath);
            }
            if (running) Theme.Glow(g, rect, 16, color, (0.35f + 0.35f * pulse) * a, 4, 1.3f);
            if (v.Flash > 0) Theme.Glow(g, rect, 16, color, v.Flash * a, 6, 1.6f);

            using var path = Theme.RoundRect(rect, 16);
            using (var fill = new LinearGradientBrush(rect, C(Theme.Lerp(Theme.Surface, Theme.SurfaceAlt, hover * 0.6f)), C(Theme.Surface), 90f))
                g.FillPath(fill, path);

            g.SetClip(path, CombineMode.Intersect);
            // Durum rengiyle köşe ışığı
            Theme.RadialLight(g, new PointF(rect.X + 30, rect.Y + 28), 120, color, (int)((item.State == TestState.Pending ? 8 : 34 + v.Flash * 60) * a));

            // Test sırasında kartın üzerinden geçen ışık süzmesi
            if (running)
            {
                var sweep = (_time * 0.7f + v.Seed) % 1f;
                var x = rect.X - 120 + (rect.Width + 240) * sweep;
                var band = new RectangleF(x - 60, rect.Y, 120, rect.Height);
                using var brush = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Theme.Alpha(Theme.Accent, 0), Theme.Alpha(Theme.Accent, (int)(22 * a)), Theme.Alpha(Theme.Accent, 0)],
                        Positions = [0f, 0.5f, 1f]
                    }
                };
                g.FillRectangle(brush, band);
            }
            g.ResetClip();

            var borderColor = Theme.Lerp(Theme.Border, color, running ? 0.4f + 0.4f * pulse : item.IsDone ? 0.35f + hover * 0.3f : hover * 0.5f);
            using (var border = new Pen(C(borderColor), 1.1f)) g.DrawPath(border, path);

            // Durum simgesi
            var iconRect = new RectangleF(rect.X + 16, rect.Y + 16, 38, 38);
            using (var iconPath = Theme.RoundRect(iconRect, 11))
            using (var iconFill = new SolidBrush(C(Theme.Alpha(color, 32))))
            {
                g.FillPath(iconFill, iconPath);
            }
            var iconState = g.Save();
            var iconScale = item.IsDone ? (float)Easing.OutBack(v.Pop.Value) : 1f;
            var icx = iconRect.X + iconRect.Width / 2f;
            var icy = iconRect.Y + iconRect.Height / 2f;
            g.TranslateTransform(icx, icy);
            if (running) g.RotateTransform(_time * 220f % 360f);
            g.ScaleTransform(Math.Max(0.01f, iconScale), Math.Max(0.01f, iconScale));
            using (var iconBrush = new SolidBrush(C(color)))
                g.DrawString(glyph, Theme.Icon(16f), iconBrush, new RectangleF(-19, -19, 38, 38), Theme.Center);
            g.Restore(iconState);

            // Rozet
            var badgeFont = Theme.Font(10f, FontStyle.Bold);
            var badgeWidth = g.MeasureString(label, badgeFont).Width + 16;
            var pop = (float)Easing.OutBack(v.Pop.Value);
            var badge = new RectangleF(rect.Right - badgeWidth - 14, rect.Y + 18, badgeWidth, 20);
            var badgeState = g.Save();
            var bcx = badge.X + badge.Width / 2f;
            var bcy = badge.Y + badge.Height / 2f;
            g.TranslateTransform(bcx, bcy);
            g.ScaleTransform(Math.Max(0.01f, pop), Math.Max(0.01f, pop));
            g.TranslateTransform(-bcx, -bcy);
            using (var badgePath = Theme.RoundRect(badge, 10))
            using (var badgeFill = new SolidBrush(C(Theme.Alpha(color, 36))))
            {
                g.FillPath(badgeFill, badgePath);
            }
            using (var badgeText = new SolidBrush(C(color))) g.DrawString(label, badgeFont, badgeText, badge, Theme.Center);
            g.Restore(badgeState);

            // Ad ve alt başlık
            var textX = iconRect.Right + 12;
            var textW = badge.X - textX - 8;
            using (var name = new SolidBrush(C(Theme.TextPrimary)))
                g.DrawString(item.Name, Theme.SemiBold(14.5f), name, new RectangleF(textX, rect.Y + 15, textW, 22), Theme.LeftCenter);
            using (var sub = new SolidBrush(C(Theme.TextMuted)))
                g.DrawString(item.Subtitle, Theme.Font(12f), sub, new RectangleF(textX, rect.Y + 36, rect.Right - textX - 14, 18), Theme.LeftCenter);

            // Uç nokta ve gecikme
            var rowY = rect.Y + 66;
            using (var endpointIcon = new SolidBrush(C(Theme.TextMuted)))
                g.DrawString(item.IsUdp ? Glyph.Wifi : Glyph.Ethernet, Theme.Icon(12f), endpointIcon, rect.X + 18, rowY + 3);
            using (var endpoint = new SolidBrush(C(Theme.TextSecondary)))
                g.DrawString(item.Endpoint + (item.IsUdp ? "  udp" : ""), Theme.Mono(12.5f), endpoint, rect.X + 38, rowY);

            if (item.LatencyMs is { } latency)
            {
                var latencyColor = latency < 50 ? Theme.Success : latency < 150 ? Theme.Warning : Theme.Danger;
                var text = $"{latency} ms";
                var font = Theme.SemiBold(12.5f);
                var tw = g.MeasureString(text, font).Width;
                using (var dot = new SolidBrush(C(latencyColor))) g.FillEllipse(dot, rect.Right - tw - 30, rowY + 6, 7, 7);
                using var latencyBrush = new SolidBrush(C(Theme.TextPrimary));
                g.DrawString(text, font, latencyBrush, rect.Right - tw - 16, rowY);
            }

            // Aşama çubukları
            var stagesY = rect.Y + 98;
            var segGap = 6f;
            var segW = (rect.Width - 32 - segGap * 3) / 4f;
            for (var s = 0; s < DeviceTestItem.StageCount; s++)
            {
                var seg = new RectangleF(rect.X + 16 + s * (segW + segGap), stagesY, segW, 5);
                using var segPath = Theme.RoundRect(seg, 2.5f);
                var stage = item.Stages[s];
                var segColor = stage switch
                {
                    StageState.Active => Theme.Lerp(Theme.Accent, Color.White, pulse * 0.25f),
                    StageState.Passed => Theme.Success,
                    StageState.Warning => Theme.Warning,
                    StageState.Skipped => Theme.TextMuted,
                    StageState.Failed => Theme.Danger,
                    _ => Theme.Border
                };
                using (var segFill = new SolidBrush(C(segColor))) g.FillPath(segFill, segPath);

                if (stage == StageState.Active)
                {
                    // Aktif aşamada akan ışık
                    g.SetClip(segPath, CombineMode.Intersect);
                    var px = seg.X - 30 + (seg.Width + 60) * ((_time * 1.3f + v.Seed) % 1f);
                    var shine = new RectangleF(px - 20, seg.Y, 40, seg.Height);
                    using var shineBrush = new LinearGradientBrush(shine, Color.Transparent, Color.Transparent, 0f)
                    {
                        InterpolationColors = new ColorBlend
                        {
                            Colors = [Color.FromArgb(0, 255, 255, 255), Color.FromArgb((int)(200 * a), 255, 255, 255), Color.FromArgb(0, 255, 255, 255)],
                            Positions = [0f, 0.5f, 1f]
                        }
                    };
                    g.FillRectangle(shineBrush, shine);
                    g.ResetClip();
                }
            }

            // Durum metni
            using var status = new SolidBrush(C(running ? Theme.TextSecondary : item.State == TestState.Fail ? Theme.Lerp(Theme.Danger, Theme.TextSecondary, 0.4f) : Theme.TextMuted));
            g.DrawString(item.StatusText, Theme.Font(12f), status, new RectangleF(rect.X + 16, rect.Y + 114, rect.Width - 32, 22), Theme.LeftCenter);
        }

        private void DrawScrollbar(Graphics g, SizeF size)
        {
            var thumb = ThumbRect(size);
            if (thumb.IsEmpty) return;
            var hover = Math.Max(_scrollbarHover.Value, _dragOffset != null ? 1 : 0);
            thumb = new RectangleF(thumb.Right - 4 - hover * 2, thumb.Y, 4 + hover * 2, thumb.Height);
            using var path = Theme.RoundRect(thumb, thumb.Width / 2f);
            using var brush = new SolidBrush(Theme.Alpha(Theme.TextMuted, 0.35f + hover * 0.45f));
            g.FillPath(brush, path);
        }

        private void DrawSkeleton(Graphics g, SizeF size, float alpha)
        {
            var available = size.Width - ScrollbarSpace;
            var columns = Math.Max(1, (int)((available + Gap) / (MinCardWidth + Gap)));
            var width = (available - (columns - 1) * Gap) / columns;
            var count = columns * 2;
            for (var i = 0; i < count; i++)
            {
                var rect = new RectangleF(i % columns * (width + Gap), i / columns * (CardHeight + Gap) + 4, width, CardHeight);
                using var path = Theme.RoundRect(rect, 16);
                using (var fill = new SolidBrush(Theme.Alpha(Theme.Surface, alpha))) g.FillPath(fill, path);
                using (var border = new Pen(Theme.Alpha(Theme.Border, alpha), 1f)) g.DrawPath(border, path);

                using var block = new SolidBrush(Theme.Alpha(Theme.SurfaceAlt, alpha));
                void Block(float x, float y, float w, float h, float r)
                {
                    using var p = Theme.RoundRect(new RectangleF(rect.X + x, rect.Y + y, w, h), r);
                    g.FillPath(block, p);
                }
                Block(16, 16, 38, 38, 11);
                Block(66, 19, width * 0.45f, 12, 6);
                Block(66, 39, width * 0.3f, 9, 4.5f);
                Block(16, 70, width * 0.4f, 10, 5);
                Block(16, 98, width - 32, 5, 2.5f);
                Block(16, 120, width * 0.55f, 9, 4.5f);

                // Işık süzmesi
                g.SetClip(path);
                var sweep = (_time * 0.8f - i * 0.08f) % 1f;
                if (sweep < 0) sweep += 1;
                var x0 = rect.X - 150 + (rect.Width + 300) * sweep;
                var band = new RectangleF(x0 - 80, rect.Y, 160, rect.Height);
                using (var shine = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Color.FromArgb(0, 255, 255, 255), Color.FromArgb((int)(16 * alpha), 255, 255, 255), Color.FromArgb(0, 255, 255, 255)],
                        Positions = [0f, 0.5f, 1f]
                    }
                })
                {
                    g.FillRectangle(shine, band);
                }
                g.ResetClip();
            }
        }

        private void DrawEmpty(Graphics g, SizeF size)
        {
            var cy = Math.Min(size.Height / 2f, 140);
            Theme.RadialLight(g, new PointF(size.Width / 2f, cy - 10), 90, Theme.Accent2, 40);
            using (var icon = new SolidBrush(Theme.TextMuted))
                g.DrawString(Glyph.Filter, Theme.Icon(34f), icon, new RectangleF(0, cy - 50, size.Width, 50), Theme.Center);
            using (var title = new SolidBrush(Theme.TextSecondary))
                g.DrawString(_items.Count == 0 ? "Henüz cihaz yok" : "Bu filtreye uyan cihaz yok", Theme.SemiBold(15f), title, new RectangleF(0, cy + 6, size.Width, 24), Theme.Center);
            using var sub = new SolidBrush(Theme.TextMuted);
            g.DrawString(_items.Count == 0 ? "Cihaz listesini yenilemeyi deneyin." : "Farklı bir filtre seçin veya aramayı temizleyin.", Theme.Font(13f), sub, new RectangleF(0, cy + 32, size.Width, 22), Theme.Center);
        }

        private sealed class CardVisual(TestState state)
        {
            private static readonly Random Random = new();

            public SmoothValue X { get; } = new(0, 9);
            public SmoothValue Y { get; } = new(0, 9);
            public SmoothValue Alpha { get; } = new(0, 9);
            public SmoothValue Hover { get; } = new(0, 14);
            public SmoothValue Pop { get; } = new(1, 7);
            public float Delay { get; set; }
            public float Flash { get; set; }
            public bool Shown { get; set; }
            public TestState LastState { get; set; } = state;
            public float Seed { get; } = (float)Random.NextDouble();
        }
    }
}
