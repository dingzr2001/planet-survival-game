using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvival))]
    public sealed class SurvivalDecay : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _hungerLossPerGameHour = 2f;
        [SerializeField, Min(0f)] private float _thirstLossPerGameHour = 3f;
        [SerializeField, Min(0f)] private float _healthLossPerEmptyVitalPerGameHour = 5f;

        private PlayerSurvival _survival;

        private void Awake()
        {
            _survival = GetComponent<PlayerSurvival>();
        }

        public void Tick(float elapsedGameHours)
        {
            if (_survival == null)
            {
                _survival = GetComponent<PlayerSurvival>();
            }

            if (_survival == null || elapsedGameHours <= 0f)
            {
                return;
            }

            _survival.Stats.Hunger.Change(-_hungerLossPerGameHour * elapsedGameHours);
            _survival.Stats.Thirst.Change(-_thirstLossPerGameHour * elapsedGameHours);

            int emptyVitalCount = CountEmptySurvivalVitals();
            if (emptyVitalCount > 0)
            {
                _survival.Apply(
                    VitalType.Health,
                    -_healthLossPerEmptyVitalPerGameHour * emptyVitalCount * elapsedGameHours);
            }
        }

        public void Configure(float hungerLossPerGameHour, float thirstLossPerGameHour, float healthLossPerEmptyVitalPerGameHour)
        {
            _hungerLossPerGameHour = Mathf.Max(0f, hungerLossPerGameHour);
            _thirstLossPerGameHour = Mathf.Max(0f, thirstLossPerGameHour);
            _healthLossPerEmptyVitalPerGameHour = Mathf.Max(0f, healthLossPerEmptyVitalPerGameHour);
        }

        private int CountEmptySurvivalVitals()
        {
            int count = 0;
            count += _survival.Stats.Sanity.Current <= 0f ? 1 : 0;
            count += _survival.Stats.Hunger.Current <= 0f ? 1 : 0;
            count += _survival.Stats.Thirst.Current <= 0f ? 1 : 0;
            return count;
        }
    }
}
