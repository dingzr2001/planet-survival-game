using System;

namespace PlanetSurvival.Oxygen.Domain
{
    /// <summary>
    /// Runtime state of one deployed oxygen candle. It settles fixed output intervals from expedition
    /// time, so the candle keeps burning while its surface scene is unloaded.
    /// </summary>
    public sealed class OxygenCandleBurn
    {
        public const float OutputIntervalGameHours = 1f;
        public const float OxygenLitersPerInterval = 100f;
        public const int OutputIntervals = 24;
        private const double GameHoursPerDay = 24d;

        private double _ignitedAtDays;
        private int _settledIntervals;

        public bool IsIgnited { get; private set; }
        public bool IsBurning => IsIgnited && _settledIntervals < OutputIntervals;
        public bool IsSpent => _settledIntervals >= OutputIntervals;
        public int SettledIntervals => _settledIntervals;
        public int RemainingIntervals => Math.Max(0, OutputIntervals - _settledIntervals);
        public float TotalOxygenLiters => OxygenLitersPerInterval * OutputIntervals;

        public event Action Changed;

        /// <summary>Ignites this candle exactly once at the supplied expedition time.</summary>
        public bool Ignite(double nowDays)
        {
            if (IsIgnited || !IsFinite(nowDays))
            {
                return false;
            }

            _ignitedAtDays = nowDays;
            IsIgnited = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Settles every whole interval elapsed by <paramref name="nowDays"/>. Output that does not fit
        /// is vented because an ignited chemical candle cannot be paused.
        /// </summary>
        public float Advance(double nowDays, OxygenReservoir reservoir)
        {
            if (!IsBurning || reservoir == null || !IsFinite(nowDays) || nowDays < _ignitedAtDays)
            {
                return 0f;
            }

            double elapsedGameHours = (nowDays - _ignitedAtDays) * GameHoursPerDay;
            int completedIntervals = Math.Min(
                OutputIntervals,
                (int)Math.Floor(elapsedGameHours / OutputIntervalGameHours + 1e-9d));
            int newlyCompleted = completedIntervals - _settledIntervals;
            if (newlyCompleted <= 0)
            {
                return 0f;
            }

            float accepted = reservoir.Fill(newlyCompleted * OxygenLitersPerInterval);
            _settledIntervals = completedIntervals;
            Changed?.Invoke();
            return accepted;
        }

        public float RemainingGameHours(double nowDays)
        {
            if (!IsBurning || !IsFinite(nowDays))
            {
                return 0f;
            }

            double totalHours = OutputIntervalGameHours * OutputIntervals;
            double elapsedHours = Math.Max(0d, (nowDays - _ignitedAtDays) * GameHoursPerDay);
            return (float)Math.Max(0d, totalHours - elapsedHours);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
