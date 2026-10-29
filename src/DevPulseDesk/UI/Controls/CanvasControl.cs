using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Tüm özel çizimli kontrollerin tabanı. Çizim 96 DPI mantıksal koordinatlarda yapılır,
    /// animasyon karesi gerektiğinde <see cref="FrameClock"/>'a abone olunur.
    /// </summary>
    internal abstract class CanvasControl : Control
    {
        private readonly Action<float> _frame;
        private bool _ticking;

        protected CanvasControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _frame = Frame;
            BackColor = Theme.Background;
            ForeColor = Theme.TextPrimary;
        }

        protected static float DpiScale => Dpi.Scale;

        protected SizeF Logical => new(Width / DpiScale, Height / DpiScale);

        protected static PointF ToLogical(Point p) => new(p.X / DpiScale, p.Y / DpiScale);

        /// <summary>True ise kontrol görünür olduğu sürece sürekli animasyon karesi alır.</summary>
        protected virtual bool AlwaysAnimate => false;

        protected void StartTicking()
        {
            if (_ticking || IsDisposed) return;
            _ticking = true;
            FrameClock.Add(_frame);
        }

        protected void StopTicking()
        {
            if (!_ticking) return;
            _ticking = false;
            FrameClock.Remove(_frame);
        }

        private void Frame(float dt)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                StopTicking();
                return;
            }

            var keepGoing = OnFrame(dt) || AlwaysAnimate;
            if (Visible) InvalidateFrame();
            if (!keepGoing) StopTicking();
        }

        /// <summary>Kare sonrası yeniden çizilecek alanı belirler; varsayılan tüm kontroldür.</summary>
        protected virtual void InvalidateFrame() => Invalidate();

        /// <summary>Her animasyon karesinde çağrılır. Animasyon sürüyorsa true döndürmelidir.</summary>
        protected virtual bool OnFrame(float dt) => false;

        protected abstract void OnRender(Graphics g, SizeF size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Handle oluşmadan önce hedefi değişen değerlerin yerine oturması için en az bir kare çalıştır.
            StartTicking();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopTicking();
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            StopTicking();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            g.ScaleTransform(DpiScale, DpiScale);
            OnRender(g, Logical);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // OnPaint tüm alanı boyadığı için arka plan çizimi atlanır (titreşimi önler).
        }
    }

    /// <summary>Titreşimsiz çift tamponlu panel.</summary>
    internal class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    /// <summary>Titreşimsiz çift tamponlu tablo düzeni.</summary>
    internal sealed class BufferedTable : TableLayoutPanel
    {
        public BufferedTable()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    internal static class Effects
    {
        private static readonly Dictionary<Control, (int Left, Tween Tween)> Shaking = new();

        /// <summary>Hatalı girişlerde kontrolü yatay olarak sallar.</summary>
        public static void Shake(Control control, float amplitude = 10f)
        {
            int baseLeft;
            if (Shaking.TryGetValue(control, out var running))
            {
                running.Tween.Cancel();
                baseLeft = running.Left;
            }
            else
            {
                baseLeft = control.Left;
            }

            var tween = Animator.Animate(520, Easing.Linear, t =>
            {
                if (control.IsDisposed) return;
                var offset = Math.Sin(t * Math.PI * 7) * amplitude * Dpi.Scale * (1 - t);
                control.Left = baseLeft + (int)offset;
            }, () =>
            {
                Shaking.Remove(control);
                if (!control.IsDisposed) control.Left = baseLeft;
            });
            Shaking[control] = (baseLeft, tween);
        }

        /// <summary>Kontrolü aşağıdan yukarı kaydırarak sahneye getirir.</summary>
        public static void SlideUp(Control control, int distance, double delayMs, double durationMs = 650)
        {
            var finalTop = control.Top;
            var offset = Dpi.S(distance);
            control.Top = finalTop + offset;
            Animator.Animate(durationMs, Easing.OutCubic, t =>
            {
                if (!control.IsDisposed) control.Top = finalTop + (int)(offset * (1 - t));
            }, delayMs: delayMs);
        }

        public static Task FadeAsync(Form form, double from, double to, double durationMs)
        {
            form.Opacity = from;
            return Animator.AnimateAsync(durationMs, Easing.OutCubic, t =>
            {
                if (!form.IsDisposed) form.Opacity = from + (to - from) * t;
            });
        }
    }
}
