using System;

namespace PlanetSurvival.Oxygen.Domain
{
    /// <summary>Stores breathable oxygen in liters without allowing negative or over-capacity state.</summary>
    public sealed class OxygenReservoir
    {
        public OxygenReservoir(float capacityLiters, float initialLiters = 0f)
        {
            if (!IsFinite(capacityLiters) || capacityLiters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityLiters), "Capacity must be finite and positive.");
            }

            if (!IsFinite(initialLiters) || initialLiters < 0f || initialLiters > capacityLiters)
            {
                throw new ArgumentOutOfRangeException(nameof(initialLiters),
                    "Initial oxygen must be finite and between zero and the reservoir capacity.");
            }

            CapacityLiters = capacityLiters;
            CurrentLiters = initialLiters;
        }

        public float CapacityLiters { get; }
        public float CurrentLiters { get; private set; }
        public float Normalized => CurrentLiters / CapacityLiters;

        public event Action<float, float> Changed;

        /// <summary>Consumes up to the requested amount and returns the amount that was available.</summary>
        public float Consume(float liters)
        {
            if (!IsFinite(liters) || liters <= 0f || CurrentLiters <= 0f)
            {
                return 0f;
            }

            float consumed = Math.Min(liters, CurrentLiters);
            SetCurrent(CurrentLiters - consumed);
            return consumed;
        }

        public void Reset(float liters)
        {
            if (!IsFinite(liters) || liters < 0f || liters > CapacityLiters)
            {
                throw new ArgumentOutOfRangeException(nameof(liters));
            }

            SetCurrent(liters);
        }

        private void SetCurrent(float liters)
        {
            if (Math.Abs(CurrentLiters - liters) <= float.Epsilon)
            {
                return;
            }

            CurrentLiters = liters;
            Changed?.Invoke(CurrentLiters, CapacityLiters);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
