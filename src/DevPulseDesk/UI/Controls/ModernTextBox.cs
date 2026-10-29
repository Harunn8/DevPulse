using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using DevPulseDesk.UI.Animation;

namespace DevPulseDesk.UI.Controls
{
    /// <summary>
    /// Yüzen etiketli (floating label), odakta merkezden genişleyen gradyan çizgili metin kutusu.
    /// </summary>
    internal sealed class ModernTextBox : CanvasControl
    {
        private const float LabelSize = 14f;
        private const float FloatingScale = 0.78f;

        private readonly TextBox _box;
        private readonly SmoothValue _float = new(0, 14);
        private readonly SmoothValue _focus = new(0, 12);
        private readonly SmoothValue _error = new(0, 10);
        private readonly SmoothValue _hover = new(0, 14);
        private bool _isPassword;
        private bool _reveal;
        private bool _expanded;

        public ModernTextBox()
        {
            // Odak her zaman içteki TextBox'ta durur; kapsayıcı sekme sırasına girmez.
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.IBeam;
            BackColor = Theme.Chrome;

            _box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = FieldColor,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Native(14.5f)
            };
            _box.TextChanged += (_, _) =>
            {
                if (_error.Target > 0) SetError(false);
                OnTextChanged(EventArgs.Empty);
            };
            _box.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Submit?.Invoke(this, EventArgs.Empty);
                }
            };
            Controls.Add(_box);
        }

        public event EventHandler? Submit;

        private static Color FieldColor => Theme.SurfaceAlt;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Label { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPassword
        {
            get => _isPassword;
            set
            {
                _isPassword = value;
                _box.UseSystemPasswordChar = value && !_reveal;
                LayoutBox();
            }
        }

        [AllowNull]
        public override string Text
        {
            get => _box?.Text ?? "";
            set
            {
                if (_box == null) return;
                _box.Text = value ?? "";
                if (_box.Text.Length > 0)
                {
                    _float.Snap(1);
                    SetExpanded(true);
                }
                Invalidate();
            }
        }

        public void SetError(bool hasError)
        {
            _error.Target = hasError ? 1 : 0;
            StartTicking();
        }

        public void SelectAll() => _box.SelectAll();

        public void FocusInput() => _box.Focus();

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }

        private float TextLeft => string.IsNullOrEmpty(Glyph) ? 18f : 48f;
        private float TextRight => _isPassword ? 46f : 16f;

        private void LayoutBox()
        {
            if (_box == null) return;
            var left = Dpi.S(TextLeft);
            var top = (int)(Height * 0.47f);
            // Etiket ortadayken kutu 1 px'e daraltılır ki etiketin üzerini örtmesin.
            var width = _expanded ? Math.Max(10, Width - left - Dpi.S(TextRight)) : 1;
            _box.SetBounds(left, top, width, _box.PreferredHeight);
        }

        private void SetExpanded(bool expanded)
        {
            if (_expanded == expanded) return;
            _expanded = expanded;
            LayoutBox();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            _focus.Target = 1;
            _float.Target = 1;
            StartTicking();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            _focus.Target = 0;
            if (_box.Text.Length == 0)
            {
                _float.Target = 0;
                SetExpanded(false);
            }
            StartTicking();
        }

        private RectangleF EyeRect(SizeF size) => new(size.Width - 42, 0, 36, size.Height);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_isPassword && EyeRect(Logical).Contains(ToLogical(e.Location)))
            {
                _reveal = !_reveal;
                _box.UseSystemPasswordChar = !_reveal;
                Invalidate();
                _box.Focus();
                return;
            }
            _box.Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _isPassword && EyeRect(Logical).Contains(ToLogical(e.Location)) ? Cursors.Hand : Cursors.IBeam;
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
            if (ClientRectangle.Contains(PointToClient(MousePosition))) return;
            _hover.Target = 0;
            StartTicking();
        }

        protected override bool OnFrame(float dt)
        {
            var moving = _float.Step(dt) | _focus.Step(dt) | _error.Step(dt) | _hover.Step(dt);
            if (!_expanded && _float.Value > 0.45f && _float.Target > 0.5f) SetExpanded(true);
            return moving;
        }

        protected override void OnRender(Graphics g, SizeF size)
        {
            var focus = _focus.Value;
            var error = _error.Value;
            var field = new RectangleF(0.75f, 0.75f, size.Width - 1.5f, size.Height - 1.5f);
            using var path = Theme.RoundRect(field, 12f);

            using (var fill = new SolidBrush(FieldColor)) g.FillPath(fill, path);

            // Odakta iç parıltı
            if (focus > 0.01f)
            {
                g.SetClip(path);
                var glowColor = Theme.Lerp(Theme.Accent, Theme.Danger, error);
                using var glow = new LinearGradientBrush(field, Theme.Alpha(glowColor, 0.10f * focus), Theme.Alpha(glowColor, 0f), 90f);
                g.FillRectangle(glow, field);
                g.ResetClip();
            }

            var borderColor = Theme.Lerp(Theme.Lerp(Theme.Border, Theme.TextMuted, _hover.Value * 0.6f), Theme.Accent, focus);
            borderColor = Theme.Lerp(borderColor, Theme.Danger, error);
            using (var pen = new Pen(Theme.Alpha(borderColor, 0.85f), 1.2f)) g.DrawPath(pen, path);

            // Merkezden genişleyen alt çizgi
            if (focus > 0.01f)
            {
                var lineWidth = (field.Width - 24) * focus;
                var cx = field.X + field.Width / 2f;
                var line = new RectangleF(cx - lineWidth / 2f, field.Bottom - 2.2f, Math.Max(1, lineWidth), 2.2f);
                using var lineBrush = new LinearGradientBrush(RectangleF.Inflate(line, 1, 0),
                    Theme.Lerp(Theme.Accent, Theme.Danger, error), Theme.Lerp(Theme.Accent2, Theme.Danger, error), 0f);
                using var linePath = Theme.RoundRect(line, 1.1f);
                g.FillPath(lineBrush, linePath);
            }

            // Simge
            if (!string.IsNullOrEmpty(Glyph))
            {
                var iconColor = Theme.Lerp(Theme.Lerp(Theme.TextMuted, Theme.Accent, focus), Theme.Danger, error);
                using var iconBrush = new SolidBrush(iconColor);
                g.DrawString(Glyph, Theme.Icon(16f), iconBrush, new RectangleF(12, 0, 28, size.Height), Theme.Center);
            }

            // Yüzen etiket
            var f = (float)Easing.OutCubic(_float.Value);
            var labelScale = Theme.Lerp(1f, FloatingScale, f);
            var labelHeight = LabelSize * 1.35f;
            var labelY = Theme.Lerp((size.Height - labelHeight) / 2f, 7f, f);
            var labelColor = Theme.Lerp(Theme.TextSecondary, Theme.Lerp(Theme.TextSecondary, Theme.Accent, focus), f);
            labelColor = Theme.Lerp(labelColor, Theme.Danger, error);
            var state = g.Save();
            g.TranslateTransform(TextLeft - 1, labelY);
            g.ScaleTransform(labelScale, labelScale);
            using (var labelBrush = new SolidBrush(labelColor))
            {
                g.DrawString(Label, Theme.Font(LabelSize), labelBrush, PointF.Empty);
            }
            g.Restore(state);

            // Şifre göster/gizle
            if (_isPassword)
            {
                var eye = EyeRect(size);
                using var eyeBrush = new SolidBrush(_reveal ? Theme.Accent : Theme.TextMuted);
                g.DrawString(_reveal ? UI.Glyph.Hide : UI.Glyph.View, Theme.Icon(15f), eyeBrush, eye, Theme.Center);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _box.Dispose();
            base.Dispose(disposing);
        }
    }
}
