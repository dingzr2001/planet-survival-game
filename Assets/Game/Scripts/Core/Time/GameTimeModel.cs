using System;

namespace PlanetSurvival.Core.Time
{
    public sealed class GameTimeModel
    {
        private readonly double _realSecondsPerGameDay;
        private long _lastReportedMinute = -1;

        public GameTimeModel(double realSecondsPerGameDay, int rescueDay, double timeScale = 1d)
        {
            if (realSecondsPerGameDay <= 0d) throw new ArgumentOutOfRangeException(nameof(realSecondsPerGameDay));
            if (rescueDay < 1) throw new ArgumentOutOfRangeException(nameof(rescueDay));
            _realSecondsPerGameDay = realSecondsPerGameDay;
            RescueDay = rescueDay;
            SetTimeScale(timeScale);
        }

        public double ElapsedDays { get; private set; }
        public int CurrentDay => (int)Math.Floor(ElapsedDays) + 1;
        public int Hour => (int)Math.Floor(NormalizedTimeOfDay * 24d);
        public int Minute => (int)Math.Floor(ElapsedDays * 24d * 60d) % 60;
        public int RescueDay { get; }
        public int DaysUntilRescue => Math.Max(0, RescueDay - CurrentDay);
        public bool RescueAvailable => CurrentDay >= RescueDay;
        public bool IsPaused { get; private set; }
        public double TimeScale { get; private set; }
        public float NormalizedTimeOfDay => (float)(ElapsedDays - Math.Floor(ElapsedDays));
        public event Action<int> DayChanged;
        public event Action TimeChanged;

        public void SetPaused(bool isPaused) => IsPaused = isPaused;

        public void SetTimeScale(double timeScale)
        {
            if (timeScale < 0d) throw new ArgumentOutOfRangeException(nameof(timeScale));
            TimeScale = timeScale;
        }

        public double Advance(double elapsedRealSeconds)
        {
            if (elapsedRealSeconds <= 0d || IsPaused || TimeScale <= 0d) return 0d;
            int previousDay = CurrentDay;
            double elapsedDays = elapsedRealSeconds * TimeScale / _realSecondsPerGameDay;
            ElapsedDays += elapsedDays;
            double elapsedGameHours = elapsedDays * 24d;
            for (int day = previousDay + 1; day <= CurrentDay; day++)
            {
                DayChanged?.Invoke(day);
            }
            long currentMinute = (long)Math.Floor(ElapsedDays * 24d * 60d);
            if (currentMinute != _lastReportedMinute)
            {
                _lastReportedMinute = currentMinute;
                TimeChanged?.Invoke();
            }
            return elapsedGameHours;
        }
    }
}
