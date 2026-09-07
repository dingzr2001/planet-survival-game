using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    [DisallowMultipleComponent]
    public sealed class PlayerSurvival : MonoBehaviour
    {
        [SerializeField] private SurvivalStats _stats = new();
        private bool _initialized;

        public SurvivalStats Stats => _stats;
        public bool IsDead { get; private set; }
        public event Action Died;

        /// <summary>Binds this scene's player facade to expedition-scoped survival state.</summary>
        public void Bind(SurvivalStats stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            if (_initialized)
            {
                _stats.Health.Changed -= HandleHealthChanged;
            }

            _stats = stats;
            _initialized = true;
            IsDead = _stats.Health.Current <= 0f;
            _stats.Health.Changed += HandleHealthChanged;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (_initialized)
            {
                _stats.Health.Changed -= HandleHealthChanged;
            }
        }

        public void Apply(VitalType type, float amount)
        {
            EnsureInitialized();
            if (IsDead)
            {
                return;
            }

            _stats.Get(type).Change(amount);
        }

        public void ConsumeCalories(int calories)
        {
            if (calories <= 0)
            {
                return;
            }

            Apply(VitalType.Hunger, NutritionBalance.ToHungerPoints(calories));
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _stats.Health.Changed += HandleHealthChanged;
            HandleHealthChanged(_stats.Health.Current, _stats.Health.Maximum);
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            if (IsDead || current > 0f)
            {
                return;
            }

            IsDead = true;
            Died?.Invoke();
        }
    }
}
