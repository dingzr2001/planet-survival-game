using System;

namespace PlanetSurvival.Water.Domain
{
    /// <summary>
    /// Stores a single liquid volume in milliliters. Transfers are clamped by both the source
    /// volume and target capacity, so callers cannot create liquid or overfill a container.
    /// </summary>
    public sealed class LiquidContainer
    {
        public LiquidContainer(int capacityMilliliters, int initialMilliliters = 0)
        {
            if (capacityMilliliters <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityMilliliters), "Capacity must be positive.");
            }

            if (initialMilliliters < 0 || initialMilliliters > capacityMilliliters)
            {
                throw new ArgumentOutOfRangeException(nameof(initialMilliliters),
                    "Initial volume must be between zero and the container capacity.");
            }

            CapacityMilliliters = capacityMilliliters;
            CurrentMilliliters = initialMilliliters;
        }

        public int CapacityMilliliters { get; }
        public int CurrentMilliliters { get; private set; }
        public int RemainingCapacityMilliliters => CapacityMilliliters - CurrentMilliliters;
        public float Normalized => (float)CurrentMilliliters / CapacityMilliliters;

        public event Action<int, int> Changed;

        /// <summary>Moves as much liquid as possible from <paramref name="source"/> into this container.</summary>
        public int FillFrom(LiquidContainer source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (ReferenceEquals(source, this) || source.CurrentMilliliters == 0 || RemainingCapacityMilliliters == 0)
            {
                return 0;
            }

            int transferred = Math.Min(source.CurrentMilliliters, RemainingCapacityMilliliters);
            source.CurrentMilliliters -= transferred;
            CurrentMilliliters += transferred;
            // Both values are committed before observers run, so a UI reading both containers can
            // never see the transfer in a half-completed state.
            source.Changed?.Invoke(source.CurrentMilliliters, source.CapacityMilliliters);
            Changed?.Invoke(CurrentMilliliters, CapacityMilliliters);
            return transferred;
        }

        public bool TryConsume(int milliliters)
        {
            if (milliliters <= 0)
            {
                return false;
            }

            if (milliliters > CurrentMilliliters)
            {
                return false;
            }

            SetCurrent(CurrentMilliliters - milliliters);
            return true;
        }

        public void Reset(int milliliters)
        {
            if (milliliters < 0 || milliliters > CapacityMilliliters)
            {
                throw new ArgumentOutOfRangeException(nameof(milliliters));
            }

            SetCurrent(milliliters);
        }

        private void SetCurrent(int milliliters)
        {
            if (CurrentMilliliters == milliliters)
            {
                return;
            }

            CurrentMilliliters = milliliters;
            Changed?.Invoke(CurrentMilliliters, CapacityMilliliters);
        }
    }
}
