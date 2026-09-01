using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    /// <summary>
    /// Applies the hypoxia rule on game time. The formula lives in <see cref="HypoxiaRule"/>;
    /// this component only assembles references, keeps the runtime rule read-only and routes
    /// losses through <see cref="PlayerSurvival.Apply"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvival))]
    [RequireComponent(typeof(PlayerOxygen))]
    public sealed class PlayerHypoxia : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Oxygen ratio below which sanity starts draining.")]
        private float _sanityThreshold = HypoxiaRule.DefaultSanityThreshold;
        [SerializeField, Range(0f, 1f), Tooltip("Oxygen ratio below which health starts draining as well.")]
        private float _healthThreshold = HypoxiaRule.DefaultHealthThreshold;
        [SerializeField, Min(0f), Tooltip("Sanity lost per game hour at zero oxygen.")]
        private float _maximumSanityLossPerGameHour = HypoxiaRule.DefaultMaximumSanityLossPerGameHour;
        [SerializeField, Min(0f), Tooltip("Health lost per game hour at zero oxygen.")]
        private float _maximumHealthLossPerGameHour = HypoxiaRule.DefaultMaximumHealthLossPerGameHour;
        [SerializeField, Min(0f)] private float _sanityExponent = HypoxiaRule.DefaultSanityExponent;
        [SerializeField, Min(0f)] private float _healthExponent = HypoxiaRule.DefaultHealthExponent;

        private PlayerSurvival _survival;
        private PlayerOxygen _oxygen;
        private HypoxiaRule _rule;
        private bool _initialized;
        private HypoxiaLevel _level = HypoxiaLevel.Safe;
        private HypoxiaResult _lastResult;

        /// <summary>Raised when the discrete hypoxia stage changes. Display only; losses use the severities.</summary>
        public event Action<HypoxiaLevel> LevelChanged;

        public HypoxiaLevel Level
        {
            get { EnsureInitialized(); return _level; }
        }

        /// <summary>Most recent evaluation, refreshed on every tick and on every oxygen change.</summary>
        public HypoxiaResult LastResult
        {
            get { EnsureInitialized(); return _lastResult; }
        }

        public HypoxiaRule Rule
        {
            get { EnsureInitialized(); return _rule; }
        }

        private void Awake() => EnsureInitialized();

        private void OnDestroy()
        {
            if (_initialized && _oxygen != null)
            {
                _oxygen.Changed -= HandleOxygenChanged;
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => _rule = null;
#endif

        public void Configure(float sanityThreshold, float healthThreshold,
            float maximumSanityLossPerGameHour, float maximumHealthLossPerGameHour,
            float sanityExponent, float healthExponent)
        {
            _sanityThreshold = sanityThreshold;
            _healthThreshold = healthThreshold;
            _maximumSanityLossPerGameHour = maximumSanityLossPerGameHour;
            _maximumHealthLossPerGameHour = maximumHealthLossPerGameHour;
            _sanityExponent = sanityExponent;
            _healthExponent = healthExponent;
            _rule = BuildRule();
            RefreshLevel();
        }

        /// <summary>Settles hypoxia for one game time step. Nothing happens on a non-positive step.</summary>
        public void Tick(float elapsedGameHours)
        {
            if (float.IsNaN(elapsedGameHours) || float.IsInfinity(elapsedGameHours))
            {
                Debug.LogError(
                    $"{nameof(PlayerHypoxia)} on '{name}' rejected a non-finite time step ({elapsedGameHours}).", this);
                return;
            }

            EnsureInitialized();
            if (!isActiveAndEnabled || _survival == null || _oxygen == null || _survival.IsDead || elapsedGameHours <= 0f)
            {
                return;
            }

            HypoxiaResult result = _rule.Evaluate(_oxygen.Normalized, elapsedGameHours);
            _lastResult = result;

            if (result.SanityLoss > 0f)
            {
                _survival.Apply(VitalType.Sanity, -result.SanityLoss);
            }

            if (result.HealthLoss > 0f)
            {
                _survival.Apply(VitalType.Health, -result.HealthLoss);
            }

            SetLevel(result.Level);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                _survival = GetComponent<PlayerSurvival>();
                _oxygen = GetComponent<PlayerOxygen>();
                _initialized = true;

                if (_oxygen != null)
                {
                    _oxygen.Changed += HandleOxygenChanged;
                }
            }

            if (_rule == null)
            {
                _rule = BuildRule();
                RefreshLevel();
            }
        }

        private void HandleOxygenChanged(float current, float maximum) => RefreshLevel();

        private void RefreshLevel()
        {
            if (_rule == null || _oxygen == null)
            {
                return;
            }

            _lastResult = _rule.Evaluate(_oxygen.Normalized, 0f);
            SetLevel(_lastResult.Level);
        }

        private void SetLevel(HypoxiaLevel level)
        {
            if (_level == level)
            {
                return;
            }

            _level = level;
            LevelChanged?.Invoke(level);
        }

        /// <summary>
        /// Normalizes the serialized configuration so an inspector typo cannot throw at runtime,
        /// and reports whatever had to be corrected.
        /// </summary>
        private HypoxiaRule BuildRule()
        {
            float sanityThreshold = Sanitize(_sanityThreshold, HypoxiaRule.DefaultSanityThreshold, nameof(_sanityThreshold));
            float healthThreshold = Sanitize(_healthThreshold, HypoxiaRule.DefaultHealthThreshold, nameof(_healthThreshold));
            float maximumSanityLoss = Sanitize(_maximumSanityLossPerGameHour,
                HypoxiaRule.DefaultMaximumSanityLossPerGameHour, nameof(_maximumSanityLossPerGameHour));
            float maximumHealthLoss = Sanitize(_maximumHealthLossPerGameHour,
                HypoxiaRule.DefaultMaximumHealthLossPerGameHour, nameof(_maximumHealthLossPerGameHour));
            float sanityExponent = Sanitize(_sanityExponent, HypoxiaRule.DefaultSanityExponent, nameof(_sanityExponent));
            float healthExponent = Sanitize(_healthExponent, HypoxiaRule.DefaultHealthExponent, nameof(_healthExponent));

            float clampedSanityThreshold = Mathf.Clamp(sanityThreshold, HypoxiaRule.MinimumThreshold, 1f);
            float clampedHealthThreshold = Mathf.Clamp(healthThreshold, HypoxiaRule.MinimumThreshold, clampedSanityThreshold);
            if (!Mathf.Approximately(clampedSanityThreshold, sanityThreshold)
                || !Mathf.Approximately(clampedHealthThreshold, healthThreshold))
            {
                Debug.LogWarning(
                    $"{nameof(PlayerHypoxia)} on '{name}' normalized its thresholds to " +
                    $"health {clampedHealthThreshold} <= sanity {clampedSanityThreshold} (0 < Th <= Ts <= 1).", this);
            }

            _sanityThreshold = clampedSanityThreshold;
            _healthThreshold = clampedHealthThreshold;
            _maximumSanityLossPerGameHour = Mathf.Max(0f, maximumSanityLoss);
            _maximumHealthLossPerGameHour = Mathf.Max(0f, maximumHealthLoss);
            _sanityExponent = Mathf.Max(0f, sanityExponent);
            _healthExponent = Mathf.Max(0f, healthExponent);

            return new HypoxiaRule(
                _sanityThreshold,
                _healthThreshold,
                _maximumSanityLossPerGameHour,
                _maximumHealthLossPerGameHour,
                _sanityExponent,
                _healthExponent);
        }

        private float Sanitize(float value, float fallback, string fieldName)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value))
            {
                return value;
            }

            Debug.LogError(
                $"{nameof(PlayerHypoxia)} on '{name}' received a non-finite '{fieldName}' ({value}); using {fallback}.", this);
            return fallback;
        }
    }
}
