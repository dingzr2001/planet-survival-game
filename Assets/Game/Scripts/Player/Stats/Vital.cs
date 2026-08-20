using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    [Serializable]
    public sealed class Vital
    {
        [SerializeField, Min(0f)] private float _baseMaximum = 100f;
        [SerializeField, Min(0f)] private float _current = 100f;

        private float _maximumModifier;
        private float _temporaryOverflow;

        public Vital(float baseMaximum, float initialValue)
        {
            _baseMaximum = Mathf.Max(0f, baseMaximum);
            _current = Mathf.Clamp(initialValue, 0f, _baseMaximum);
        }

        public float Current => _current;
        public float BaseMaximum => _baseMaximum;
        public float EffectiveMaximum => Mathf.Max(0f, _baseMaximum + _maximumModifier);
        public float AllowedMaximum => EffectiveMaximum + Mathf.Max(0f, _temporaryOverflow);
        public float Normalized => EffectiveMaximum <= 0f ? 0f : Mathf.Clamp01(_current / EffectiveMaximum);

        public event Action<float, float> Changed;

        public void Change(float amount)
        {
            SetCurrent(_current + amount);
        }

        public void SetCurrent(float value)
        {
            float nextValue = Mathf.Clamp(value, 0f, AllowedMaximum);
            if (Mathf.Approximately(nextValue, _current))
            {
                return;
            }

            _current = nextValue;
            Changed?.Invoke(_current, EffectiveMaximum);
        }

        public void SetBaseMaximum(float value, bool preserveRatio = false)
        {
            float previousMaximum = EffectiveMaximum;
            float previousRatio = previousMaximum <= 0f ? 0f : _current / previousMaximum;
            _baseMaximum = Mathf.Max(0f, value);
            _current = Mathf.Clamp(preserveRatio ? EffectiveMaximum * previousRatio : _current, 0f, AllowedMaximum);
            Changed?.Invoke(_current, EffectiveMaximum);
        }

        public void SetMaximumModifier(float modifier)
        {
            _maximumModifier = modifier;
            _current = Mathf.Clamp(_current, 0f, AllowedMaximum);
            Changed?.Invoke(_current, EffectiveMaximum);
        }

        public void SetTemporaryOverflow(float amount)
        {
            _temporaryOverflow = Mathf.Max(0f, amount);
            SetCurrent(_current);
        }
    }
}
