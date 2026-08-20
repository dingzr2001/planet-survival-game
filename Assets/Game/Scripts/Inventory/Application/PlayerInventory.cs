using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Inventory.Application
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _totalCapacity = 30;

        private PlayerSurvival _survival;

        public Domain.Inventory Inventory { get; private set; }
        public QuickBarConfiguration QuickBar { get; private set; }

        private void Awake()
        {
            _survival = GetComponent<PlayerSurvival>();
            Inventory = new Domain.Inventory(_totalCapacity);
            QuickBar = new QuickBarConfiguration(Inventory);
        }

        private void OnDestroy()
        {
            QuickBar?.Dispose();
        }

        public InventoryOperationResult Use(string stackId)
        {
            ItemStack stack = Inventory.FindStack(stackId);
            if (stack == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.StackNotFound, $"Stack '{stackId}' was not found.");
            }

            if (!stack.Definition.CanUse)
            {
                return InventoryOperationResult.Fail(InventoryFailure.ItemCannotBeUsed, $"Item '{stack.Definition.ItemId}' cannot be used.");
            }

            if (_survival == null || _survival.IsDead)
            {
                return InventoryOperationResult.Fail(InventoryFailure.EffectRejected, "The actor cannot receive item effects.");
            }

            for (int i = 0; i < stack.Definition.Effects.Count; i++)
            {
                var effect = stack.Definition.Effects[i];
                _survival.Apply(effect.VitalType, effect.Amount);
            }

            return Inventory.Remove(stackId, 1);
        }
    }
}
