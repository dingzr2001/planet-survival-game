using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Suit.Runtime;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    /// <summary>Consumes oxygen at a fixed rate from the suit when equipped, otherwise from the room supply.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSurvival))]
    [RequireComponent(typeof(PlayerOxygen))]
    [RequireComponent(typeof(PlayerSpaceSuit))]
    public sealed class PlayerOxygenConsumption : MonoBehaviour
    {
        public const float DefaultConsumptionLitersPerGameHour = 5f;

        [SerializeField, Min(0f), Tooltip("Oxygen consumed per game hour, in liters.")]
        private float _consumptionLitersPerGameHour = DefaultConsumptionLitersPerGameHour;

        private PlayerOxygen _playerOxygen;
        private PlayerSurvival _survival;
        private PlayerSpaceSuit _spaceSuit;
        private OxygenReservoir _landingPodOxygen;
        private OxygenReservoir _activeSupply;

        public float ConsumptionLitersPerGameHour => _consumptionLitersPerGameHour;
        public OxygenReservoir ActiveSupply => _activeSupply;

        public void Bind(PlayerOxygen playerOxygen, PlayerSpaceSuit spaceSuit,
            OxygenReservoir landingPodOxygen = null)
        {
            Unsubscribe();
            _playerOxygen = playerOxygen;
            _survival = GetComponent<PlayerSurvival>();
            _spaceSuit = spaceSuit;
            _landingPodOxygen = landingPodOxygen;

            if (_survival == null || _playerOxygen == null || _spaceSuit == null || _spaceSuit.Resources == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerOxygenConsumption)} on '{name}' requires oxygen state and a bound space suit.",
                    this);
                enabled = false;
                return;
            }

            enabled = true;
            _spaceSuit.EquippedChanged += HandleEquippedChanged;
            SelectActiveSupply();
        }

        public void ConfigureRate(float litersPerGameHour)
        {
            if (float.IsNaN(litersPerGameHour) || float.IsInfinity(litersPerGameHour) || litersPerGameHour < 0f)
            {
                Debug.LogError($"{nameof(PlayerOxygenConsumption)} on '{name}' rejected an invalid rate.", this);
                return;
            }

            _consumptionLitersPerGameHour = litersPerGameHour;
        }

        public void Tick(float elapsedGameHours)
        {
            if (float.IsNaN(elapsedGameHours) || float.IsInfinity(elapsedGameHours))
            {
                Debug.LogError($"{nameof(PlayerOxygenConsumption)} on '{name}' rejected a non-finite time step.", this);
                return;
            }

            if (!isActiveAndEnabled || elapsedGameHours <= 0f || _activeSupply == null || _survival.IsDead)
            {
                return;
            }

            _activeSupply.Consume(_consumptionLitersPerGameHour * elapsedGameHours);
            SynchronizePlayerOxygen();
        }

        private void OnDestroy() => Unsubscribe();

        private void HandleEquippedChanged(bool isEquipped) => SelectActiveSupply();

        private void SelectActiveSupply()
        {
            if (_activeSupply != null)
            {
                _activeSupply.Changed -= HandleSupplyChanged;
            }

            _activeSupply = _spaceSuit != null && _spaceSuit.IsEquipped
                ? _spaceSuit.Resources?.Oxygen
                : _landingPodOxygen;

            if (_activeSupply != null)
            {
                _activeSupply.Changed += HandleSupplyChanged;
            }

            SynchronizePlayerOxygen();
        }

        private void HandleSupplyChanged(float current, float capacity) => SynchronizePlayerOxygen();

        private void SynchronizePlayerOxygen()
        {
            if (_playerOxygen != null)
            {
                _playerOxygen.SetNormalized(_activeSupply?.Normalized ?? 0f);
            }
        }

        private void Unsubscribe()
        {
            if (_spaceSuit != null)
            {
                _spaceSuit.EquippedChanged -= HandleEquippedChanged;
            }

            if (_activeSupply != null)
            {
                _activeSupply.Changed -= HandleSupplyChanged;
            }

            _activeSupply = null;
        }
    }
}
