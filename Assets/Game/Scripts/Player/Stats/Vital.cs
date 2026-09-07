using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PlanetSurvival.Player.Stats
{
    [Serializable]
    public sealed class Vital
    {
        [SerializeField, Min(0f), FormerlySerializedAs("_baseMaximum")]
        private float _maximum = 100f;
        [SerializeField, Min(0f)] private float _current = 100f;

        public Vital(float maximum, float initialValue)
        {
            _maximum = Mathf.Max(0f, maximum);
            _current = Mathf.Clamp(initialValue, 0f, _maximum);
        }

        public float Current => _current;
        public float Maximum => Mathf.Max(0f, _maximum);

        /// <summary>Current value as a ratio of the maximum. A maximum of zero reads as empty, not as full.</summary>
        public float Normalized => Maximum <= 0f ? 0f : Mathf.Clamp01(_current / Maximum);

        /// <summary>Raised only when the current value actually moved, with (current, maximum).</summary>
        public event Action<float, float> Changed;

        public void Change(float amount)
        {
            SetCurrent(_current + amount);
        }

        /// <summary>Sets the current value, clamped to [0, maximum].</summary>
        public void SetCurrent(float value)
        {
            float nextValue = Mathf.Clamp(value, 0f, Maximum);
            if (Mathf.Approximately(nextValue, _current))
            {
                return;
            }

            _current = nextValue;
            Changed?.Invoke(_current, Maximum);
        }

        /// <summary>Sets the maximum and clamps the current value into the new range.</summary>
        public void SetMaximum(float value)
        {
            _maximum = Mathf.Max(0f, value);
            _current = Mathf.Clamp(_current, 0f, _maximum);
            Changed?.Invoke(_current, _maximum);
        }
    }
}
