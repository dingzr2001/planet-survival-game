using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    [Serializable]
    public sealed class SurvivalStats
    {
        [SerializeField] private Vital _health = new(100f, 100f);
        [SerializeField] private Vital _sanity = new(100f, 100f);
        [SerializeField] private Vital _hunger = new(100f, 100f);
        [SerializeField] private Vital _thirst = new(100f, 100f);
        [SerializeField] private Vital _oxygen = new(100f, 100f);

        public Vital Health => _health;
        public Vital Sanity => _sanity;
        public Vital Hunger => _hunger;
        public Vital Thirst => _thirst;
        public Vital Oxygen => _oxygen;

        public void Reset()
        {
            _health.SetCurrent(_health.Maximum);
            _sanity.SetCurrent(_sanity.Maximum);
            _hunger.SetCurrent(_hunger.Maximum);
            _thirst.SetCurrent(_thirst.Maximum);
            _oxygen.SetCurrent(_oxygen.Maximum);
        }

        public Vital Get(VitalType type)
        {
            return type switch
            {
                VitalType.Health => _health,
                VitalType.Sanity => _sanity,
                VitalType.Hunger => _hunger,
                VitalType.Thirst => _thirst,
                VitalType.Oxygen => _oxygen,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
