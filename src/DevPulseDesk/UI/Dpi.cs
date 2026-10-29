namespace DevPulseDesk.UI
{
    /// <summary>
    /// Tüm arayüz 96 DPI'lık mantıksal birimlerle tasarlanır; bu sınıf sistem DPI'ına göre ölçekler.
    /// </summary>
    internal static class Dpi
    {
        private static float? _scale;

        public static float Scale
        {
            get
            {
                if (_scale == null)
                {
                    using var g = Graphics.FromHwnd(IntPtr.Zero);
                    _scale = Math.Max(1f, g.DpiX / 96f);
                }
                return _scale.Value;
            }
        }

        public static int S(float value) => (int)Math.Round(value * Scale);

        public static Size S(int width, int height) => new(S(width), S(height));

        public static Padding S(int left, int top, int right, int bottom) => new(S(left), S(top), S(right), S(bottom));
    }
}
