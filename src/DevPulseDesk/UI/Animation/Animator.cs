namespace DevPulseDesk.UI.Animation
{
    internal static class Easing
    {
        public static double Linear(double t) => t;
        public static double InCubic(double t) => t * t * t;
        public static double OutCubic(double t) => 1 - Math.Pow(1 - t, 3);
        public static double OutQuint(double t) => 1 - Math.Pow(1 - t, 5);
        public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

        public static double OutBack(double t)
        {
            const double c1 = 1.70158, c3 = c1 + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
        }

        public static double OutElastic(double t)
        {
            const double c4 = 2 * Math.PI / 3;
            return t is 0 or 1 ? t : Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
        }
    }

    internal sealed class Tween
    {
        internal double Elapsed;
        internal required double Delay;
        internal required double Duration;
        internal required Func<double, double> Ease;
        internal required Action<double> Update;
        internal Action? Done;

        public bool IsFinished { get; internal set; }

        public void Cancel() => IsFinished = true;
    }

    /// <summary>Süreye dayalı (tween) animasyon motoru.</summary>
    internal static class Animator
    {
        private static readonly List<Tween> Active = new();
        private static readonly Action<float> FrameCallback = OnFrame;

        public static Tween Animate(double durationMs, Func<double, double> ease, Action<double> update, Action? done = null, double delayMs = 0)
        {
            var tween = new Tween { Delay = delayMs, Duration = Math.Max(1, durationMs), Ease = ease, Update = update, Done = done };
            Active.Add(tween);
            FrameClock.Add(FrameCallback);
            return tween;
        }

        public static Task AnimateAsync(double durationMs, Func<double, double> ease, Action<double> update, double delayMs = 0)
        {
            var tcs = new TaskCompletionSource();
            Animate(durationMs, ease, update, () => tcs.TrySetResult(), delayMs);
            return tcs.Task;
        }

        private static void OnFrame(float dt)
        {
            foreach (var tween in Active.ToArray())
            {
                if (tween.IsFinished)
                {
                    Active.Remove(tween);
                    continue;
                }

                tween.Elapsed += dt * 1000.0;
                if (tween.Elapsed < tween.Delay) continue;

                var progress = Math.Clamp((tween.Elapsed - tween.Delay) / tween.Duration, 0, 1);
                try
                {
                    tween.Update(tween.Ease(progress));
                }
                catch (ObjectDisposedException)
                {
                    tween.IsFinished = true;
                    Active.Remove(tween);
                    continue;
                }

                if (progress >= 1)
                {
                    tween.IsFinished = true;
                    Active.Remove(tween);
                    tween.Done?.Invoke();
                }
            }

            if (Active.Count == 0) FrameClock.Remove(FrameCallback);
        }
    }

    /// <summary>Hedef değere üstel yumuşatma ile yaklaşan değer (hover, sayaç vb. için).</summary>
    internal sealed class SmoothValue
    {
        public SmoothValue(float initial = 0f, float speed = 12f)
        {
            Value = Target = initial;
            Speed = speed;
        }

        public float Value { get; set; }
        public float Target { get; set; }
        public float Speed { get; set; }

        public bool IsSettled => Math.Abs(Target - Value) < 0.0005f;

        public void Snap(float value) => Value = Target = value;

        /// <returns>Hâlâ hareket ediyorsa true.</returns>
        public bool Step(float dt)
        {
            var delta = Target - Value;
            if (Math.Abs(delta) < 0.0005f * Math.Max(1f, Math.Abs(Target)))
            {
                Value = Target;
                return false;
            }
            Value += delta * (1f - MathF.Exp(-Speed * dt));
            return true;
        }
    }
}
