using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [CreateAssetMenu(menuName = "Planet Survival/World/Environment Settings")]
    public sealed class PlanetEnvironmentSettings : ScriptableObject
    {
        [Header("Time")]
        [SerializeField, Min(1f), Tooltip("Real seconds required for one complete game day.")]
        private float _realSecondsPerGameDay = 600f;
        [SerializeField, Min(1)] private int _rescueDay = 30;
        [Header("Atmosphere")]
        [SerializeField, Range(0f, 1f)] private float _surfaceOxygen = 0.21f;
        [SerializeField, Min(1f)] private float _oxygenFalloffHeight = 1000f;
        [Header("Day and night")]
        [SerializeField] private Gradient _sunColor = new();
        [SerializeField] private AnimationCurve _sunIntensity = new();
        [SerializeField] private Gradient _ambientColor = new();
        [SerializeField, Range(0f, 360f)] private float _sunYaw = 330f;

        public float RealSecondsPerGameDay => _realSecondsPerGameDay;
        public int RescueDay => _rescueDay;
        public AtmosphereModel CreateAtmosphere() => new(_surfaceOxygen, _oxygenFalloffHeight);
        public Color EvaluateSunColor(float timeOfDay) => _sunColor.Evaluate(timeOfDay);
        public float EvaluateSunIntensity(float timeOfDay) => Mathf.Max(0f, _sunIntensity.Evaluate(timeOfDay));
        public Color EvaluateAmbientColor(float timeOfDay) => _ambientColor.Evaluate(timeOfDay);
        public float SunYaw => _sunYaw;

        public void ConfigureDefaults()
        {
            _sunColor = CreateGradient(new Color(1f, .35f, .2f), new Color(1f, .95f, .82f), new Color(1f, .3f, .18f));
            _ambientColor = CreateGradient(new Color(.025f, .035f, .08f), new Color(.55f, .65f, .8f), new Color(.025f, .035f, .08f));
            _sunIntensity = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.23f, 0f), new Keyframe(.3f, .8f), new Keyframe(.5f, 1.1f), new Keyframe(.7f, .8f), new Keyframe(.77f, 0f), new Keyframe(1f, 0f));
        }

        private static Gradient CreateGradient(Color start, Color middle, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, .5f), new GradientColorKey(end, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
