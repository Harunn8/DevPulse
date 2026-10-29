using System.Drawing.Drawing2D;
using DevPulseDesk.Services;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Terminal görünümlü canlı günlük: yeni satırlar kayarak gelir, liste yumuşakça yukarı akar,
    /// fare tekerleği ile geçmişe dönülebilir.
    /// </summary>
    internal sealed class LogConsole : CanvasControl
    {
        private const float HeaderHeight = 50f;
        private const float LineHeight = 21f;
        private const int MaxEntries = 600;

        private readonly List<Entry> _entries = new();
        private readonly SmoothValue _scroll = new(0, 14);
        private readonly SmoothValue _shift = new(0, 14);
        private readonly SmoothValue _clearHover = new(0, 14);
        private float _time;

        public LogConsole()
        {
            BackColor = Theme.Background;
        }

        public void Add(LogLevel level, string text)
        {
            _entries.Add(new Entry(DateTime.Now.ToString("HH:mm:ss"), level, text, _time));
            if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);

            if (_scroll.Target > 0.01f)
            {
                // Kullanıcı geçmişe bakıyorsa görünümü sabit tut.
                _scroll.Target += 1;
                _scroll.Value += 1;
            }
            else
            {
                _shift.Value += 1;
                _shift.Target = 0;
            }
            StartTicking();
        }

        public void Clear()
        {
            _entries.Clear();
            _scroll.Snap(0);
            _shift.Snap(0);
            Invalidate();
        }

        private RectangleF Body(SizeF size) => new(14, HeaderHeight, size.Width - 28, size.Height - HeaderHeight - 12);

        private int VisibleLines(SizeF size) => (int)(Body(size).Height / LineHeight);

        private RectangleF ClearRect(SizeF size) => new(size.Width - 80, 14, 64, 24);

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var max = Math.Max(0, _entries.Count - VisibleLines(Logical));
            _scroll.Target = Math.Clamp(_scroll.Target + e.Delta / 120f * 3, 0, max);
            StartTicking();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var over = ClearRect(Logical).Contains(ToLogical(e.Location));
            _clearHover.Target = over ? 1 : 0;
            Cursor = over ? Cursors.Hand : Cursors.Default;
            StartTicking();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _clearHover.Target = 0;
            StartTicking();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (ClearRect(Logical).Contains(ToLogical(e.Location))) Clear();
        }

        protected override bool OnFrame(float dt)
        {
            _time += dt;
            return _scroll.Step(dt) | _shift.Step(dt) | _clearHover.Step(dt);
        }

        // "CANLI" göstergesi ve imleç sürekli yanıp söner.
        protected override bool AlwaysAnimate => true;

        protected override void OnRender(Graphics g, SizeF size)
        {
            var card = new RectangleF(0.5f, 0.5f, size.Width - 1, size.Height - 1);
            using (var path = Theme.RoundRect(card, 18))
            {
                using var fill = new SolidBrush(Theme.Terminal);
                g.FillPath(fill, path);
                using var border = new Pen(Theme.Border, 1f);
                g.DrawPath(border, path);
            }

            // Başlık
            using (var icon = new SolidBrush(Theme.Accent))
                g.DrawString("", Theme.Icon(15f), icon, 20, 18); // CommandPrompt
            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString("Canlı Günlük", Theme.SemiBold(15.5f), title, 44, 15);

            var blink = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_time * 5));
            var liveX = 154f;
            using (var liveFill = new SolidBrush(Theme.Alpha(Theme.Danger, 34)))
            using (var livePath = Theme.RoundRect(new RectangleF(liveX, 18, 46, 18), 9))
            {
                g.FillPath(liveFill, livePath);
            }
            using (var dot = new SolidBrush(Theme.Alpha(Theme.Danger, blink))) g.FillEllipse(dot, liveX + 7, 24, 6, 6);
            using (var liveText = new SolidBrush(Theme.Danger)) g.DrawString("CANLI", Theme.Font(9.5f, FontStyle.Bold), liveText, liveX + 15, 20);

            var clear = ClearRect(size);
            using (var clearPath = Theme.RoundRect(clear, 8))
            using (var clearFill = new SolidBrush(Theme.Alpha(Theme.SurfaceHover, _clearHover.Value)))
            {
                g.FillPath(clearFill, clearPath);
            }
            using (var clearText = new SolidBrush(Theme.Lerp(Theme.TextMuted, Theme.TextPrimary, _clearHover.Value)))
                g.DrawString("Temizle", Theme.Font(12f), clearText, clear, Theme.Center);

            using (var separator = new Pen(Theme.Alpha(Theme.Border, 160), 1f))
                g.DrawLine(separator, 14, HeaderHeight - 6, size.Width - 14, HeaderHeight - 6);

            var body = Body(size);
            if (_entries.Count == 0)
            {
                using var muted = new SolidBrush(Theme.TextMuted);
                var cursorOn = (int)(_time * 2) % 2 == 0;
                g.DrawString("> günlük bekleniyor" + (cursorOn ? "_" : ""), Theme.Mono(12.5f), muted, body.X + 4, body.Y + 4);
                return;
            }

            var state = g.Save();
            g.SetClip(body);
            var font = Theme.Mono(12f);
            var bottom = body.Bottom - 4;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var fromBottom = _entries.Count - 1 - i;
                var y = bottom - (fromBottom + 1 - _scroll.Value) * LineHeight + _shift.Value * LineHeight;
                if (y > body.Bottom) continue;
                if (y + LineHeight < body.Y) break;
                DrawEntry(g, _entries[i], body.X, y, body.Width, font);
            }
            g.Restore(state);

            // Üstte eski satırları solduran maske
            var fadeRect = new RectangleF(body.X, body.Y - 1, body.Width, 26);
            using (var fade = new LinearGradientBrush(fadeRect, Theme.Terminal, Theme.Alpha(Theme.Terminal, 0), 90f))
                g.FillRectangle(fade, fadeRect);

            if (_scroll.Target > 0.5f)
            {
                var pill = new RectangleF(size.Width / 2f - 70, size.Height - 36, 140, 24);
                using var path = Theme.RoundRect(pill, 12);
                using var fill = new SolidBrush(Theme.Alpha(Theme.Accent2, 200));
                g.FillPath(fill, path);
                using var text = new SolidBrush(Color.White);
                g.DrawString("↓ yeni kayıtlar", Theme.Font(11.5f), text, pill, Theme.Center);
            }
        }

        private void DrawEntry(Graphics g, Entry entry, float x, float y, float width, Font font)
        {
            var age = _time - entry.Born;
            var appear = (float)Easing.OutCubic(Math.Clamp(age / 0.4f, 0, 1));
            var offset = (1 - appear) * 16f;

            var (tag, color) = entry.Level switch
            {
                LogLevel.Success => (" OK ", Theme.Success),
                LogLevel.Error => ("FAIL", Theme.Danger),
                LogLevel.Warning => ("WARN", Theme.Warning),
                LogLevel.System => ("SYS ", Theme.Accent2),
                _ => ("INFO", Theme.Accent)
            };

            // Yeni gelen satırı kısa süre vurgula
            if (age < 0.8f)
            {
                using var highlight = new SolidBrush(Theme.Alpha(color, (int)((1 - age / 0.8f) * 26)));
                g.FillRectangle(highlight, x, y + 1, width, LineHeight - 2);
            }

            using (var time = new SolidBrush(Theme.Alpha(Theme.TextMuted, appear)))
                g.DrawString(entry.Time, font, time, x + 4 + offset, y + 2);

            var tagRect = new RectangleF(x + 74 + offset, y + 3, 40, LineHeight - 6);
            using (var tagPath = Theme.RoundRect(tagRect, 4))
            using (var tagFill = new SolidBrush(Theme.Alpha(color, (int)(36 * appear))))
            {
                g.FillPath(tagFill, tagPath);
            }
            using (var tagText = new SolidBrush(Theme.Alpha(color, appear)))
                g.DrawString(tag, Theme.Mono(10.5f, FontStyle.Bold), tagText, tagRect, Theme.Center);

            var textColor = entry.Level is LogLevel.Error or LogLevel.Success ? Theme.TextPrimary : Theme.TextSecondary;
            using var text = new SolidBrush(Theme.Alpha(textColor, appear));
            g.DrawString(entry.Text, font, text, new RectangleF(tagRect.Right + 8, y, width - (tagRect.Right - x) - 12, LineHeight), Theme.LeftCenter);
        }

        private sealed record Entry(string Time, LogLevel Level, string Text, float Born);
    }
}
