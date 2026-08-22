using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [System.Serializable]
    public sealed class AtmosphereModel
    {
        [SerializeField, Range(0f, 1f)] private float _surfaceOxygen = 0.21f;
        [SerializeField, Min(1f)] private float _oxygenFalloffHeight = 1000f;

        public float SurfaceOxygen => _surfaceOxygen;
        public float OxygenFalloffHeight => _oxygenFalloffHeight;

        public AtmosphereModel() { }

        public AtmosphereModel(float surfaceOxygen, float oxygenFalloffHeight)
        {
            _surfaceOxygen = Mathf.Clamp01(surfaceOxygen);
            _oxygenFalloffHeight = Mathf.Max(1f, oxygenFalloffHeight);
        }

        public float GetOxygenRatio(float heightAboveSurface)
        {
            float height = Mathf.Max(0f, heightAboveSurface);
            return _surfaceOxygen * Mathf.Exp(-height / _oxygenFalloffHeight);
        }
    }
}
