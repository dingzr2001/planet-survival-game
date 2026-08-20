using System;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Core.Time
{
    [DisallowMultipleComponent]
    public sealed class GameClock : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _realSecondsPerGameDay = 600f;
        [SerializeField, Min(1)] private int _rescueDay = 30;

        private SurvivalDecay _survivalDecay;
        private int _lastReportedMinute = -1;

        public float ElapsedDays { get; private set; }
        public int CurrentDay => Mathf.FloorToInt(ElapsedDays) + 1;
        public int Hour => Mathf.FloorToInt((ElapsedDays % 1f) * 24f);
        public int Minute => Mathf.FloorToInt((ElapsedDays * 24f * 60f) % 60f);
        public int RescueDay => _rescueDay;
        public int DaysUntilRescue => Mathf.Max(0, _rescueDay - CurrentDay);
        public bool RescueAvailable => CurrentDay >= _rescueDay;
        public event Action<int> DayChanged;
        public event Action TimeChanged;

        public void Bind(SurvivalDecay survivalDecay)
        {
            _survivalDecay = survivalDecay;
        }

        public void Configure(float realSecondsPerGameDay, int rescueDay)
        {
            _realSecondsPerGameDay = Mathf.Max(1f, realSecondsPerGameDay);
            _rescueDay = Mathf.Max(1, rescueDay);
        }

        private void Update()
        {
            Advance(UnityEngine.Time.deltaTime);
        }

        public void Advance(float elapsedRealSeconds)
        {
            if (elapsedRealSeconds <= 0f)
            {
                return;
            }

            int previousDay = CurrentDay;
            float elapsedDays = elapsedRealSeconds / _realSecondsPerGameDay;
            ElapsedDays += elapsedDays;
            _survivalDecay?.Tick(elapsedDays * 24f);

            if (CurrentDay != previousDay)
            {
                DayChanged?.Invoke(CurrentDay);
            }


            int currentMinute = Mathf.FloorToInt(ElapsedDays * 24f * 60f);
            if (currentMinute != _lastReportedMinute)
            {
                _lastReportedMinute = currentMinute;
                TimeChanged?.Invoke();
            }
        }
    }
}
