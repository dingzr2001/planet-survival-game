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
        [SerializeField, Min(0f)] private float _timeScale = 1f;
        private GameTimeModel _time;
        private SurvivalDecay _survivalDecay;
        private PlayerHypoxia _hypoxia;
        private PlayerOxygenConsumption _oxygenConsumption;

        public double ElapsedDays => Time.ElapsedDays;
        public int CurrentDay => Time.CurrentDay;
        public int Hour => Time.Hour;
        public int Minute => Time.Minute;
        public int RescueDay => Time.RescueDay;
        public int DaysUntilRescue => Time.DaysUntilRescue;
        public bool RescueAvailable => Time.RescueAvailable;
        public bool IsPaused => Time.IsPaused;
        public float TimeScale => (float)Time.TimeScale;
        public float NormalizedTimeOfDay => Time.NormalizedTimeOfDay;
        public event Action<int> DayChanged;
        public event Action TimeChanged;

        private GameTimeModel Time
        {
            get { EnsureInitialized(); return _time; }
        }

        /// <summary>
        /// Drives this scene's clock from an expedition-scoped time model instead of a private one, so
        /// elapsed days survive a scene change. The model outlives the clock, which is why the
        /// subscription is released again in <see cref="OnDestroy"/>.
        /// </summary>
        public void Bind(GameTimeModel time)
        {
            if (time == null)
            {
                Debug.LogError($"{nameof(GameClock)} on '{name}' cannot bind a null time model.", this);
                return;
            }

            if (ReferenceEquals(_time, time))
            {
                return;
            }

            if (_time != null)
            {
                Unsubscribe(_time);
            }

            _time = time;
            _time.DayChanged += ForwardDayChanged;
            _time.TimeChanged += ForwardTimeChanged;
        }

        public void Bind(SurvivalDecay survivalDecay) => _survivalDecay = survivalDecay;

        public void Bind(PlayerHypoxia hypoxia) => _hypoxia = hypoxia;

        public void Bind(PlayerOxygenConsumption oxygenConsumption) => _oxygenConsumption = oxygenConsumption;

        public void Configure(float realSecondsPerGameDay, int rescueDay, float timeScale = 1f)
        {
            _realSecondsPerGameDay = Mathf.Max(1f, realSecondsPerGameDay);
            _rescueDay = Mathf.Max(1, rescueDay);
            _timeScale = Mathf.Max(0f, timeScale);
            ReplaceModel();
        }

        public void SetPaused(bool isPaused) => Time.SetPaused(isPaused);

        public void SetTimeScale(float timeScale)
        {
            _timeScale = Mathf.Max(0f, timeScale);
            Time.SetTimeScale(_timeScale);
        }

        private void Awake() => EnsureInitialized();
        private void Update() => Advance(UnityEngine.Time.deltaTime);

        private void OnDestroy()
        {
            if (_time != null)
            {
                Unsubscribe(_time);
            }
        }

        public void Advance(float elapsedRealSeconds)
        {
            double elapsedGameHours = Time.Advance(elapsedRealSeconds);
            if (elapsedGameHours <= 0d)
            {
                return;
            }

            var elapsedHours = (float)elapsedGameHours;
            if (_survivalDecay != null)
            {
                _survivalDecay.Tick(elapsedHours);
            }

            // Settle the breathable supply first so hypoxia observes the state reached this tick.
            if (_oxygenConsumption != null)
            {
                _oxygenConsumption.Tick(elapsedHours);
            }

            if (_hypoxia != null)
            {
                _hypoxia.Tick(elapsedHours);
            }
        }

        private void EnsureInitialized()
        {
            if (_time == null) ReplaceModel();
        }

        private void ReplaceModel()
        {
            if (_time != null) Unsubscribe(_time);
            _time = new GameTimeModel(_realSecondsPerGameDay, _rescueDay, _timeScale);
            _time.DayChanged += ForwardDayChanged;
            _time.TimeChanged += ForwardTimeChanged;
        }

        private void Unsubscribe(GameTimeModel time)
        {
            time.DayChanged -= ForwardDayChanged;
            time.TimeChanged -= ForwardTimeChanged;
        }

        private void ForwardDayChanged(int day) => DayChanged?.Invoke(day);
        private void ForwardTimeChanged() => TimeChanged?.Invoke();
    }
}
