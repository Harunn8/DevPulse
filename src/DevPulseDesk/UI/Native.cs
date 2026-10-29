using System.Runtime.InteropServices;

namespace DevPulseDesk.UI
{
    internal static class Native
    {
        public const int WM_NCHITTEST = 0x84;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 2;
        public const int WS_MINIMIZEBOX = 0x20000;
        public const int CS_DROPSHADOW = 0x20000;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Kenarlıksız pencereyi fare ile sürüklemeye başlar.</summary>
        public static void DragWindow(Form? form)
        {
            if (form == null || form.WindowState == FormWindowState.Maximized) return;
            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }

        /// <summary>Windows 11'de yuvarlatılmış köşeler ve koyu çerçeve uygular; eski sürümlerde sessizce yok sayılır.</summary>
        public static void ApplyModernWindowStyle(IntPtr handle)
        {
            try
            {
                var round = DWMWCP_ROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
                var dark = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
            }
        }
    }
}
