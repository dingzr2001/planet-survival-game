using PlanetSurvival.Water.Domain;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Water.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerWaterBottle : MonoBehaviour
    {
        public const int DrinkVolumeMilliliters = 100;
        public const float ThirstRestoredPercentage = 20f;

        private PlayerSurvival _survival;

        public LiquidContainer Container { get; private set; }
        public bool CanDrink => Container != null
                                && Container.CurrentMilliliters >= DrinkVolumeMilliliters
                                && _survival != null
                                && !_survival.IsDead
                                && _survival.Stats.Thirst.Current < _survival.Stats.Thirst.Maximum;

        public void Bind(LiquidContainer container, PlayerSurvival survival)
        {
            Container = container;
            _survival = survival;
            if (Container != null && _survival != null)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(PlayerWaterBottle)} on '{name}' requires a liquid container and player survival state.",
                this);
            enabled = false;
        }

        public WaterDrinkResult TryDrink()
        {
            if (Container == null)
            {
                return WaterDrinkResult.BottleNotBound;
            }

            if (_survival == null || _survival.IsDead)
            {
                return WaterDrinkResult.ActorUnavailable;
            }

            Vital thirst = _survival.Stats.Thirst;
            if (thirst.Current >= thirst.Maximum)
            {
                return WaterDrinkResult.NotThirsty;
            }

            if (!Container.TryConsume(DrinkVolumeMilliliters))
            {
                return WaterDrinkResult.NotEnoughWater;
            }

            _survival.Apply(VitalType.Thirst, thirst.Maximum * ThirstRestoredPercentage / 100f);
            return WaterDrinkResult.Succeeded;
        }
    }
}
