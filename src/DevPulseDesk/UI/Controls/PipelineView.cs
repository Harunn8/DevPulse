using System.Diagnostics;
using System.Drawing.Drawing2D;
using DevPulseDesk.Services;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Test aşamalarını canlı gösteren akış paneli: genel ilerleme halkası, aşama düğümleri
    /// (aktifken yayılan halkalar), aralarında akan veri paketleri ve anlık bilgi şeridi.
    /// </summary>
    internal sealed class PipelineView : CanvasControl
    {
        public enum RunState { Idle, Running, Completed, Cancelled }

        private static readonly (string Title, string Glyph, string Hint)[] Stages =
        [
            ("Adres Çözümleme", Glyph.Globe, "IP / DNS doğrulama"),
            ("ICMP Ping", Glyph.Wifi, "Gecikme ölçümü"),
            ("TCP Port", Glyph.Ethernet, "Servis erişimi"),
            ("Raporlama", Glyph.Report, "Sonuç üretimi")
        ];

        private readonly int[] _active = new int[4];
        private readonly int[] _passed = new int[4];
        private readonly int[] _failed = new int[4];
        private readonly SmoothValue[] _activity = [new(0, 8), new(0, 8), new(0, 8), new(0, 8)];
        private readonly SmoothValue[] _completion = [new(0, 6), new(0, 6), new(0, 6), new(0, 6)];
        private readonly SmoothValue _progress = new(0, 5);
        private readonly SmoothValue _detailFade = new(1, 8);
        private readonly Stopwatch _elapsed = new();
        private RunState _state = RunState.Idle;
        private int _total;
        private int _done;
        private int _success;
        private int _fail;
        private string _detail = "Başlatmak için \"Testi Başlat\" butonuna tıklayın.";
        private LogLevel _detailLevel = LogLevel.Info;
        private float _time;

        public PipelineView()
        {
            BackColor = Theme.Background;
        }

        public RunState State => _state;

        public void Reset(int total)
        {
            _state = RunState.Idle;
            _total = total;
            _done = _success = _fail = 0;
            Array.Clear(_active);
            Array.Clear(_passed);
            Array.Clear(_failed);
            foreach (var v in _completion) v.Target = 0;
            _progress.Target = 0;
            _elapsed.Reset();
            SetDetail(total > 0 ? $"{total} cihaz teste hazır. Başlatmak için \"Testi Başlat\" butonuna tıklayın." : "Test edilecek cihaz bulunamadı.", LogLevel.Info);
            StartTicking();
        }

        public void Begin(int total)
        {
            Reset(total);
            _state = RunState.Running;
            _elapsed.Restart();
            SetDetail("Test oturumu başlatıldı, cihazlar kuyruğa alınıyor…", LogLevel.System);
        }

        public void Update(IReadOnlyList<DeviceTestItem> items, string? detail, LogLevel level)
        {
            Array.Clear(_active);
            Array.Clear(_passed);
            Array.Clear(_failed);
            _done = _success = _fail = 0;
            foreach (var item in items)
            {
                for (var s = 0; s < 4; s++)
                {
                    switch (item.Stages[s])
                    {
                        case StageState.Active: _active[s]++; break;
                        case StageState.Passed or StageState.Warning or StageState.Skipped: _passed[s]++; break;
                        case StageState.Failed: _failed[s]++; break;
                    }
                }
                if (item.State == TestState.Success) _success++;
                if (item.State == TestState.Fail) _fail++;
            }
            _done = _success + _fail;
            _total = items.Count;

            // Bir cihaz erken aşamada başarısız olursa sonraki aşamalar da "işlenmiş" sayılır.
            for (var s = 0; s < 4; s++)
            {
                var processed = _passed[s] + _failed[s];
                var failedEarlier = s == 0 ? 0 : items.Count(i => i.IsDone && i.Stages[s] == StageState.Waiting);
                _completion[s].Target = _total == 0 ? 0 : (processed + failedEarlier) / (float)_total;
            }
            _progress.Target = _total == 0 ? 0 : _done / (float)_total;
            if (detail != null) SetDetail(detail, level);
            StartTicking();
        }

        public void Complete(bool cancelled)
        {
            _state = cancelled ? RunState.Cancelled : RunState.Completed;
            _elapsed.Stop();
            Array.Clear(_active);
            SetDetail(cancelled
                ? $"Test kullanıcı tarafından durduruldu • {_done}/{_total} cihaz tamamlandı"
                : $"Test tamamlandı • {_success} başarılı, {_fail} başarısız • {_elapsed.Elapsed.TotalSeconds:0.0} sn",
                cancelled ? LogLevel.Warning : _fail == 0 ? LogLevel.Success : LogLevel.Warning);
            StartTicking();
        }

        private void SetDetail(string detail, LogLevel level)
        {
            if (detail == _detail) return;
            _detail = detail;
            _detailLevel = level;
            _detailFade.Snap(0);
            _detailFade.Target = 1;
        }

        protected override bool OnFrame(float dt)
        {
            _time += dt;
            var moving = _progress.Step(dt) | _detailFade.Step(dt);
            for (var i = 0; i < 4; i++)
            {
                _activity[i].Target = _active[i] > 0 ? 1 : 0;
                moving |= _activity[i].Step(dt) | _completion[i].Step(dt);
            }
            return moving || _state == RunState.Running;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var card = new RectangleF(0.5f, 0.5f, size.Width - 1, size.Height - 1);
            using (var path = Theme.RoundRect(card, 18))
            {
                using var fill = new LinearGradientBrush(card, Theme.Surface, Color.FromArgb(14, 19, 34), 90f);
                g.FillPath(fill, path);
                g.SetClip(path);
                if (_state == RunState.Running)
                {
                    Theme.RadialLight(g, new PointF(110, size.Height / 2f + 16), 170, Theme.Accent2, (int)(40 + 20 * MathF.Sin(_time * 2)));
                }
                g.ResetClip();
                using var border = new Pen(Theme.Border, 1f);
                g.DrawPath(border, path);
            }

            DrawHeader(g, size);
            DrawRing(g, new PointF(112, 40 + (size.Height - 40 - 50) / 2f + 6));
            DrawStages(g, size);
            DrawDetail(g, size);
        }

        private void DrawHeader(Graphics g, SizeF size)
        {
            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString("Test Akışı", Theme.SemiBold(17f), title, 22, 16);

            var (label, color) = _state switch
            {
                RunState.Running => ("ÇALIŞIYOR", Theme.Accent),
                RunState.Completed => ("TAMAMLANDI", _fail == 0 ? Theme.Success : Theme.Warning),
                RunState.Cancelled => ("DURDURULDU", Theme.Warning),
                _ => ("HAZIR", Theme.TextSecondary)
            };
            var font = Theme.Font(10.5f, FontStyle.Bold);
            var w = g.MeasureString(label, font).Width + 26;
            var chip = new RectangleF(118, 19, w, 21);
            using (var path = Theme.RoundRect(chip, 10.5f))
            using (var fill = new SolidBrush(Theme.Alpha(color, 30)))
            {
                g.FillPath(fill, path);
            }
            var pulse = _state == RunState.Running ? 0.4f + 0.6f * (0.5f + 0.5f * MathF.Sin(_time * 6)) : 1f;
            using (var dot = new SolidBrush(Theme.Alpha(color, pulse))) g.FillEllipse(dot, chip.X + 9, chip.Y + 7.5f, 6, 6);
            using (var text = new SolidBrush(color)) g.DrawString(label, font, text, new RectangleF(chip.X + 18, chip.Y, w - 18, chip.Height), Theme.LeftCenter);

            var elapsed = _elapsed.Elapsed;
            var elapsedText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
            using var muted = new SolidBrush(Theme.TextSecondary);
            var timeFont = Theme.Mono(13.5f);
            var tw = g.MeasureString(elapsedText, timeFont).Width;
            g.DrawString(elapsedText, timeFont, muted, size.Width - 22 - tw, 19);
            using var icon = new SolidBrush(Theme.TextMuted);
            g.DrawString(Glyph.Clock, Theme.Icon(13f), icon, size.Width - 44 - tw, 21);
        }

        private void DrawRing(Graphics g, PointF center)
        {
            const float radius = 60f;
            const float thickness = 9f;
            var rect = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);

            using (var track = new Pen(Theme.Alpha(Theme.Border, 200), thickness)) g.DrawEllipse(track, rect);

            var progress = _progress.Value;
            if (_state == RunState.Running && _done == 0)
            {
                // Belirsiz döngü: ilk sonuç gelene kadar dönen yay
                using var brush = new LinearGradientBrush(rect, Theme.Accent, Theme.Accent2, 45f);
                using var pen = new Pen(brush, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, rect, _time * 280f % 360f, 70f);
            }
            else if (progress > 0.002f)
            {
                var sweep = 360f * progress;
                using var glowBrush = new LinearGradientBrush(rect, Theme.Alpha(Theme.Accent, 50), Theme.Alpha(Theme.Accent2, 50), 45f);
                using var glow = new Pen(glowBrush, thickness + 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(glow, rect, -90, sweep);
                using var brush = new LinearGradientBrush(rect, Theme.Accent, Theme.Accent2, 45f);
                using var pen = new Pen(brush, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, rect, -90, sweep);

                // Yay ucundaki parlak nokta
                var angle = (-90 + sweep) * MathF.PI / 180f;
                var tip = new PointF(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
                Theme.RadialLight(g, tip, 14, Theme.Accent2, 170);
            }

            using var white = new SolidBrush(Theme.TextPrimary);
            g.DrawString($"{progress * 100:0}%", Theme.SemiBold(27f), white, new RectangleF(rect.X, rect.Y + 30, rect.Width, 36), Theme.Center);
            using var muted = new SolidBrush(Theme.TextSecondary);
            g.DrawString($"{_done} / {_total} cihaz", Theme.Font(12f), muted, new RectangleF(rect.X - 10, rect.Y + 66, rect.Width + 20, 18), Theme.Center);
        }

        private void DrawStages(Graphics g, SizeF size)
        {
            var areaX = 205f;
            var areaW = size.Width - areaX - 18;
            var spacing = areaW / 4f;
            var nodeY = 48 + (size.Height - 48 - 56) * 0.36f;
            const float r = 25f;

            var centers = Enumerable.Range(0, 4).Select(i => new PointF(areaX + spacing * (i + 0.5f), nodeY)).ToArray();

            // Bağlantılar
            for (var i = 0; i < 3; i++)
            {
                var from = new PointF(centers[i].X + r + 6, nodeY);
                var to = new PointF(centers[i + 1].X - r - 6, nodeY);
                var length = to.X - from.X;
                if (length <= 4) continue;

                using (var track = new Pen(Theme.Border, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(track, from, to);

                var fill = Math.Clamp(_completion[i].Value, 0, 1);
                if (fill > 0.01f)
                {
                    var end = new PointF(from.X + length * fill, nodeY);
                    using var brush = new LinearGradientBrush(new RectangleF(from.X - 1, nodeY - 2, length + 2, 4), Theme.Accent, Theme.Accent2, 0f);
                    using var pen = new Pen(brush, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(pen, from, end);
                }

                // Akan veri paketleri
                var flow = Math.Max(_activity[i].Value, _activity[i + 1].Value);
                if (_state == RunState.Running && flow > 0.02f)
                {
                    for (var k = 0; k < 3; k++)
                    {
                        var p = (_time * 0.9f + k / 3f) % 1f;
                        var x = from.X + length * p;
                        var a = (int)(MathF.Sin(p * MathF.PI) * 255 * flow);
                        Theme.RadialLight(g, new PointF(x, nodeY), 9, Theme.Accent, a / 2);
                        using var dot = new SolidBrush(Theme.Alpha(Color.White, a));
                        g.FillEllipse(dot, x - 2.5f, nodeY - 2.5f, 5, 5);
                    }
                }
            }

            for (var i = 0; i < 4; i++) DrawNode(g, i, centers[i], r, spacing);
        }

        private void DrawNode(Graphics g, int index, PointF c, float r, float spacing)
        {
            var activity = _activity[index].Value;
            var completed = _total > 0 && _completion[index].Value > 0.995f && _active[index] == 0;
            var stageColor = completed ? (_failed[index] > 0 ? Theme.Warning : Theme.Success) : Theme.Accent;

            // Aktif aşamada yayılan halkalar
            if (activity > 0.02f)
            {
                for (var k = 0; k < 2; k++)
                {
                    var p = (_time * 0.8f + k * 0.5f) % 1f;
                    var rr = r + 4 + p * 22;
                    using var ring = new Pen(Theme.Alpha(Theme.Accent, (int)((1 - p) * 120 * activity)), 1.6f);
                    g.DrawEllipse(ring, c.X - rr, c.Y - rr, rr * 2, rr * 2);
                }
                Theme.RadialLight(g, c, r * 2.2f, Theme.Accent, (int)(70 * activity));
            }

            var node = new RectangleF(c.X - r, c.Y - r, r * 2, r * 2);
            using (var bg = new SolidBrush(Theme.Lerp(Theme.SurfaceAlt, Theme.Lerp(Theme.SurfaceAlt, stageColor, 0.22f), Math.Max(activity, completed ? 1 : 0))))
                g.FillEllipse(bg, node);

            var borderColor = completed ? stageColor : Theme.Lerp(Theme.Border, Theme.Accent, activity);
            using (var border = new Pen(borderColor, 2f)) g.DrawEllipse(border, node);

            // Aktifken dönen yay
            if (activity > 0.02f)
            {
                using var arc = new Pen(Theme.Alpha(Color.White, activity * 0.9f), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(arc, RectangleF.Inflate(node, 4, 4), _time * 320f % 360f, 80f);
            }

            var glyph = completed ? (_failed[index] > 0 ? Glyph.Warning : Glyph.Check) : Stages[index].Glyph;
            var glyphColor = completed ? stageColor : Theme.Lerp(Theme.TextMuted, Color.White, activity);
            using (var icon = new SolidBrush(glyphColor)) g.DrawString(glyph, Theme.Icon(17f), icon, node, Theme.Center);

            // Aktif cihaz sayısı rozeti
            if (_active[index] > 0)
            {
                var badgeText = _active[index].ToString();
                var badge = new RectangleF(c.X + r * 0.45f, c.Y - r - 4, 20, 18);
                using var path = Theme.RoundRect(badge, 9);
                using var fill = new LinearGradientBrush(badge, Theme.Accent, Theme.Accent2, 0f);
                g.FillPath(fill, path);
                using var text = new SolidBrush(Color.White);
                g.DrawString(badgeText, Theme.SemiBold(11f), text, badge, Theme.Center);
            }

            var labelRect = new RectangleF(c.X - spacing / 2f + 4, c.Y + r + 12, spacing - 8, 20);
            using (var title = new SolidBrush(Theme.Lerp(Theme.TextSecondary, Theme.TextPrimary, Math.Max(activity, completed ? 1 : 0))))
                g.DrawString(Stages[index].Title, Theme.SemiBold(13f), title, labelRect, Theme.Center);

            // Başarılı / başarısız sayaçları
            var countsY = labelRect.Bottom + 2;
            if (_passed[index] + _failed[index] > 0)
            {
                var font = Theme.Font(12f);
                var passText = _passed[index].ToString();
                var failText = _failed[index].ToString();
                var pw = g.MeasureString(passText, font).Width;
                var fw = g.MeasureString(failText, font).Width;
                var total = 10 + pw + 12 + 10 + fw;
                var x = c.X - total / 2f;
                using (var ok = new SolidBrush(Theme.Success)) g.FillEllipse(ok, x, countsY + 5, 7, 7);
                using (var okText = new SolidBrush(Theme.TextSecondary)) g.DrawString(passText, font, okText, x + 9, countsY);
                x += 10 + pw + 12;
                using (var bad = new SolidBrush(_failed[index] > 0 ? Theme.Danger : Theme.TextMuted)) g.FillEllipse(bad, x, countsY + 5, 7, 7);
                using (var badText = new SolidBrush(Theme.TextSecondary)) g.DrawString(failText, font, badText, x + 9, countsY);
            }
            else
            {
                using var hint = new SolidBrush(Theme.TextMuted);
                g.DrawString(Stages[index].Hint, Theme.Font(11.5f), hint, new RectangleF(labelRect.X, countsY, labelRect.Width, 18), Theme.Center);
            }
        }

        private void DrawDetail(Graphics g, SizeF size)
        {
            var strip = new RectangleF(205, size.Height - 50, size.Width - 205 - 18, 34);
            using (var path = Theme.RoundRect(strip, 10))
            using (var fill = new SolidBrush(Theme.Alpha(Theme.Background, 160)))
            {
                g.FillPath(fill, path);
            }

            var color = _detailLevel switch
            {
                LogLevel.Success => Theme.Success,
                LogLevel.Error => Theme.Danger,
                LogLevel.Warning => Theme.Warning,
                LogLevel.System => Theme.Accent2,
                _ => Theme.Accent
            };
            var fade = _detailFade.Value;
            var glyph = _detailLevel switch
            {
                LogLevel.Success => Glyph.Check,
                LogLevel.Error => Glyph.Cancel,
                LogLevel.Warning => Glyph.Warning,
                _ => _state == RunState.Running ? Glyph.Sync : Glyph.Info
            };

            var iconRect = new RectangleF(strip.X + 8, strip.Y, 24, strip.Height);
            var state = g.Save();
            if (glyph == Glyph.Sync)
            {
                var cx = iconRect.X + iconRect.Width / 2f;
                var cy = iconRect.Y + iconRect.Height / 2f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(_time * 200f % 360f);
                g.TranslateTransform(-cx, -cy);
            }
            using (var icon = new SolidBrush(Theme.Alpha(color, fade))) g.DrawString(glyph, Theme.Icon(13f), icon, iconRect, Theme.Center);
            g.Restore(state);

            using var text = new SolidBrush(Theme.Alpha(Theme.TextSecondary, fade));
            g.DrawString(_detail, Theme.Font(12.5f), text, new RectangleF(strip.X + 36 - (1 - fade) * 10, strip.Y, strip.Width - 46, strip.Height), Theme.LeftCenter);
        }
    }
}
