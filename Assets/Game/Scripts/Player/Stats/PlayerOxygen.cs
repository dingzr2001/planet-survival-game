using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    /// <summary>
    /// Scene-facing gateway to the player's oxygen vital. Every future oxygen source or drain
    /// (atmosphere, tanks, suits, story events) goes through these semantic operations instead of
    /// touching the vital directly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvival))]
    public sealed class PlayerOxygen : MonoBehaviour
    {
        private PlayerSurvival _survival;
        private Vital _oxygen;
        private bool _initialized;

        /// <summary>Raised after the current value actually changed, with (current, maximum).</summary>
        public event Action<float, float> Changed;

        public float Current => Oxygen.Current;
        public float Maximum => Oxygen.Maximum;
        public float Normalized => Oxygen.Normalized;

        private Vital Oxygen
        {
            get { EnsureInitialized(); return _oxygen; }
        }

        private void Awake() => EnsureInitialized();

        private void OnDestroy()
        {
            if (_initialized)
            {
                _oxygen.Changed -= HandleOxygenChanged;
            }
        }

        /// <summary>Removes oxygen. Negative or non-finite amounts are rejected.</summary>
        public void Consume(float amount)
        {
            if (!IsUsableAmount(amount, nameof(Consume)))
            {
                return;
            }

            Oxygen.Change(-amount);
        }

        /// <summary>Adds oxygen, clamped by the vital's allowed maximum.</summary>
        public void Restore(float amount)
        {
            if (!IsUsableAmount(amount, nameof(Restore)))
            {
                return;
            }

            Oxygen.Change(amount);
        }

        /// <summary>Sets the current value. Clamped to [0, maximum]; non-finite values are rejected.</summary>
        public void SetCurrent(float value)
        {
            if (!IsFinite(value, nameof(SetCurrent), nameof(value)))
            {
                return;
            }

            Oxygen.SetCurrent(value);
        }

        /// <summary>Sets the current value from a ratio of the maximum. Ratio is clamped to [0, 1].</summary>
        public void SetNormalized(float ratio)
        {
            if (!IsFinite(ratio, nameof(SetNormalized), nameof(ratio)))
            {
                return;
            }

            Oxygen.SetCurrent(Oxygen.Maximum * Mathf.Clamp01(ratio));
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _survival = GetComponent<PlayerSurvival>();
            _oxygen = _survival.Stats.Oxygen;
            _initialized = true;
            _oxygen.Changed += HandleOxygenChanged;
        }

        private void HandleOxygenChanged(float current, float maximum) => Changed?.Invoke(current, maximum);

        private bool IsUsableAmount(float amount, string operation)
        {
            if (!IsFinite(amount, operation, nameof(amount)))
            {
                return false;
            }

            if (amount < 0f)
            {
                Debug.LogError(
                    $"{nameof(PlayerOxygen)}.{operation} on '{name}' rejected a negative amount ({amount}). " +
                    $"Use {nameof(Consume)} or {nameof(Restore)} to pick a direction.", this);
                return false;
            }

            return true;
        }

        private bool IsFinite(float value, string operation, string parameterName)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value))
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(PlayerOxygen)}.{operation} on '{name}' rejected a non-finite '{parameterName}' ({value}).", this);
            return false;
        }

#if UNITY_EDITOR
        // Temporary manual-verification entry points; no oxygen source exists yet.
        // Right-click the component header in the Inspector while playing.
        [ContextMenu("Debug/Set Oxygen 100%")] private void DebugSetFull() => DebugSetRatio(1f);
        [ContextMenu("Debug/Set Oxygen 50%")] private void DebugSetHalf() => DebugSetRatio(.5f);
        [ContextMenu("Debug/Set Oxygen 40%")] private void DebugSetForty() => DebugSetRatio(.4f);
        [ContextMenu("Debug/Set Oxygen 30%")] private void DebugSetThirty() => DebugSetRatio(.3f);
        [ContextMenu("Debug/Set Oxygen 20%")] private void DebugSetTwenty() => DebugSetRatio(.2f);
        [ContextMenu("Debug/Set Oxygen 10%")] private void DebugSetTen() => DebugSetRatio(.1f);
        [ContextMenu("Debug/Set Oxygen 0%")] private void DebugSetEmpty() => DebugSetRatio(0f);

        private void DebugSetRatio(float ratio)
        {
            SetNormalized(ratio);
            Debug.Log($"{nameof(PlayerOxygen)} on '{name}' set to {Current:0.##} / {Maximum:0.##} ({Normalized * 100f:0}%).", this);
        }
#endif
    }
}
