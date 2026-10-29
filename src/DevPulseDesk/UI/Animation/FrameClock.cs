using System.Diagnostics;

namespace DevPulseDesk.UI.Animation
{
    /// <summary>
    /// Tüm animasyonların paylaştığı tek UI zamanlayıcısı (~60 fps).
    /// Abone kalmadığında kendini durdurur, böylece boşta CPU tüketmez.
    /// </summary>
    internal static class FrameClock
    {
        private static readonly System.Windows.Forms.Timer Timer = new() { Interval = 15 };
        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        private static readonly List<Action<float>> Subscribers = new();
        private static double _last;

        static FrameClock()
        {
            Timer.Tick += OnTick;
        }

        /// <summary>Uygulama başından beri geçen süre (saniye).</summary>
        public static float Time => (float)Watch.Elapsed.TotalSeconds;

        public static void Add(Action<float> callback)
        {
            if (!Subscribers.Contains(callback)) Subscribers.Add(callback);
            if (!Timer.Enabled)
            {
                _last = Watch.Elapsed.TotalSeconds;
                Timer.Start();
            }
        }

        public static void Remove(Action<float> callback)
        {
            Subscribers.Remove(callback);
            if (Subscribers.Count == 0) Timer.Stop();
        }

        private static void OnTick(object? sender, EventArgs e)
        {
            var now = Watch.Elapsed.TotalSeconds;
            var dt = (float)Math.Min(0.1, now - _last);
            _last = now;
            foreach (var callback in Subscribers.ToArray())
            {
                callback(dt);
            }
        }
    }
}
