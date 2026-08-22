using PlanetSurvival.Core.Time;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [DisallowMultipleComponent]
    public sealed class DayNightEnvironment : MonoBehaviour
    {
        private GameClock _clock;
        private Light _sun;
        private PlanetEnvironmentSettings _settings;

        public void Bind(GameClock clock, Light sun, PlanetEnvironmentSettings settings)
        {
            Unsubscribe();
            _clock = clock;
            _sun = sun;
            _settings = settings;
            if (_clock == null || _sun == null || _settings == null)
            {
                Debug.LogError($"{nameof(DayNightEnvironment)} requires a clock, directional light and environment settings.", this);
                enabled = false;
                return;
            }
            _clock.TimeChanged += Refresh;
            Refresh();
        }

        private void OnDestroy() => Unsubscribe();

        private void Refresh()
        {
            float time = _clock.NormalizedTimeOfDay;
            _sun.transform.rotation = Quaternion.Euler(time * 360f - 90f, _settings.SunYaw, 0f);
            _sun.color = _settings.EvaluateSunColor(time);
            _sun.intensity = _settings.EvaluateSunIntensity(time);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = _settings.EvaluateAmbientColor(time);
        }

        private void Unsubscribe()
        {
            if (_clock != null) _clock.TimeChanged -= Refresh;
        }
    }
}
