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

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _stats.Health.Changed += HandleHealthChanged;
            HandleHealthChanged(_stats.Health.Current, _stats.Health.EffectiveMaximum);
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
