using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>Panel başlığındaki zamana göre selamlama ("İyi akşamlar, kullanıcı") ve alt bilgi.</summary>
    internal sealed class GreetingBlock : CanvasControl
    {
        private readonly SmoothValue _appear = new(0, 5);

        public GreetingBlock()
        {
            BackColor = Theme.Background;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string UserName { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Info { get; set; } = "";

        public void Appear()
        {
            _appear.Snap(0);
            _appear.Target = 1;
            StartTicking();
        }

        public static string Greeting(DateTime now) => now.Hour switch
        {
            < 6 => "İyi geceler",
            < 12 => "Günaydın",
            < 18 => "İyi günler",
            _ => "İyi akşamlar"
        };

        protected override bool OnFrame(float dt) => _appear.Step(dt);

        protected override void OnRender(Graphics g, SizeF size)
        {
            var a = _appear.Value;
            var offset = (1 - a) * 20f;
            var greeting = Greeting(DateTime.Now) + ", ";
            var font = Theme.SemiBold(26f);

            using (var brush = new SolidBrush(Theme.Alpha(Theme.TextPrimary, a)))
                g.DrawString(greeting, font, brush, offset, 6);

            var gw = g.MeasureString(greeting, font).Width - 8;
            var nameWidth = g.MeasureString(UserName, font).Width;
            var nameRect = new RectangleF(offset + gw, 6, Math.Max(1, nameWidth), 40);
            using (var gradient = new LinearGradientBrush(nameRect, Theme.Alpha(Theme.Accent, a), Theme.Alpha(Theme.Accent3, a), 0f)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = [Theme.Alpha(Theme.Accent, a), Theme.Alpha(Theme.Accent2, a), Theme.Alpha(Theme.Accent3, a)],
                    Positions = [0f, 0.5f, 1f]
                }
            })
            {
                g.DrawString(UserName, font, gradient, nameRect.X, nameRect.Y);
            }

            var info = $"{DateTime.Now.ToString("d MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"))}   •   {Info}";
            using var sub = new SolidBrush(Theme.Alpha(Theme.TextSecondary, a));
            g.DrawString(info, Theme.Font(13.5f), sub, new RectangleF(offset * 1.6f + 2, 46, size.Width, 22), Theme.LeftCenter);
        }
    }

    /// <summary>Login kartının başlığı: güvenli oturum çipi, başlık ve açıklama.</summary>
    internal sealed class LoginHeader : CanvasControl
    {
        private float _time;

        public LoginHeader()
        {
            BackColor = Theme.Chrome;
        }

        protected override bool AlwaysAnimate => true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "Tekrar hoş geldiniz";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get; set; } = "Devam etmek için DevPulse hesabınızla oturum açın.";

        protected override bool OnFrame(float dt)
        {
            _time += dt;
            return true;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var chipText = "GÜVENLİ OTURUM";
            var chipFont = Theme.Font(10.5f, FontStyle.Bold);
            var chipWidth = g.MeasureString(chipText, chipFont).Width + 36;
            var chip = new RectangleF(1, 1, chipWidth, 24);
            using (var path = Theme.RoundRect(chip, 12))
            {
                using var fill = new SolidBrush(Theme.Alpha(Theme.Accent, 22));
                g.FillPath(fill, path);
                using var border = new Pen(Theme.Alpha(Theme.Accent, 60 + (int)(40 * MathF.Sin(_time * 2.5f))), 1f);
                g.DrawPath(border, path);
            }
            using (var icon = new SolidBrush(Theme.Accent))
                g.DrawString(Glyph.Shield, Theme.Icon(11f), icon, new RectangleF(chip.X + 6, chip.Y, 20, chip.Height), Theme.Center);
            using (var text = new SolidBrush(Theme.Accent))
                g.DrawString(chipText, chipFont, text, new RectangleF(chip.X + 26, chip.Y, chipWidth, chip.Height), Theme.LeftCenter);

            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString(Title, Theme.SemiBold(29f), title, -2, 38);
            using var sub = new SolidBrush(Theme.TextSecondary);
            g.DrawString(Subtitle, Theme.Font(14f), sub, new RectangleF(0, 82, size.Width, 40));
        }
    }

    /// <summary>Hata/bilgi mesajlarını yumuşakça açılıp kapanan bir şerit olarak gösterir.</summary>
    internal sealed class InfoBanner : CanvasControl
    {
        private readonly SmoothValue _visible = new(0, 10);
        private string _message = "";
        private Color _color = Theme.Danger;
        private string _glyph = Glyph.Warning;

        public InfoBanner()
        {
            BackColor = Theme.Chrome;
        }

        public void ShowMessage(string message, bool isError = true)
        {
            _message = message;
            _color = isError ? Theme.Danger : Theme.Accent;
            _glyph = isError ? Glyph.Warning : Glyph.Info;
            _visible.Snap(0);
            _visible.Target = 1;
            StartTicking();
        }

        public void HideMessage()
        {
            _visible.Target = 0;
            StartTicking();
        }

        protected override bool OnFrame(float dt) => _visible.Step(dt);

        protected override void OnRender(Graphics g, SizeF size)
        {
            var a = _visible.Value;
            if (a < 0.01f) return;
            var rect = new RectangleF(0.5f, 0.5f + (1 - a) * 8, size.Width - 1, size.Height - 1);
            using var path = Theme.RoundRect(rect, 10);
            using (var fill = new SolidBrush(Theme.Alpha(_color, (int)(28 * a)))) g.FillPath(fill, path);
            using (var border = new Pen(Theme.Alpha(_color, (int)(90 * a)), 1f)) g.DrawPath(border, path);
            using (var icon = new SolidBrush(Theme.Alpha(_color, a)))
                g.DrawString(_glyph, Theme.Icon(14f), icon, new RectangleF(rect.X + 8, rect.Y, 28, rect.Height), Theme.Center);
            using var text = new SolidBrush(Theme.Alpha(Theme.Lerp(_color, Color.White, 0.55f), a));
            g.DrawString(_message, Theme.Font(13f), text, new RectangleF(rect.X + 40, rect.Y, rect.Width - 48, rect.Height), Theme.LeftCenter);
        }
    }
    /// <summary>Cihaz ızgarasının üstündeki bölüm başlığı ve durum renk açıklamaları.</summary>
    internal sealed class SectionHeader : CanvasControl
    {
        private static readonly (string Label, Color Color)[] Legend =
        [
            ("Başarılı", Theme.Success),
            ("Başarısız", Theme.Danger),
            ("Test ediliyor", Theme.Accent),
            ("Bekliyor", Theme.TextMuted)
        ];

        public SectionHeader()
        {
            BackColor = Theme.Background;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "Cihazlar";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Info { get; set; } = "";

        protected override void OnRender(Graphics g, SizeF size)
        {
            var titleFont = Theme.SemiBold(17f);
            using (var title = new SolidBrush(Theme.TextPrimary))
                g.DrawString(Title, titleFont, title, new RectangleF(0, 0, 200, size.Height), Theme.LeftCenter);
            var tw = g.MeasureString(Title, titleFont).Width;
            using (var info = new SolidBrush(Theme.TextMuted))
                g.DrawString(Info, Theme.Font(13f), info, new RectangleF(tw + 6, 0, size.Width / 2f, size.Height), Theme.LeftCenter);

            var font = Theme.Font(12f);
            var x = size.Width - 4;
            for (var i = Legend.Length - 1; i >= 0; i--)
            {
                var (label, color) = Legend[i];
                var w = g.MeasureString(label, font).Width;
                x -= w;
                using (var text = new SolidBrush(Theme.TextSecondary))
                    g.DrawString(label, font, text, new RectangleF(x, 0, w + 4, size.Height), Theme.LeftCenter);
                x -= 12;
                using (var dot = new SolidBrush(color)) g.FillEllipse(dot, x, size.Height / 2f - 4, 8, 8);
                x -= 18;
            }
        }
    }
}
