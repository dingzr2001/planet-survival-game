using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Inventory.Application
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _totalCapacity = 30;

        private PlayerSurvival _survival;
        private Domain.Inventory _inventory;
        private QuickBarConfiguration _quickBar;

        public Domain.Inventory Inventory
        {
            get { EnsureInitialized(); return _inventory; }
        }

        public QuickBarConfiguration QuickBar
        {
            get { EnsureInitialized(); return _quickBar; }
        }

        private void Awake()
        {
            _survival = GetComponent<PlayerSurvival>();
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            _quickBar?.Dispose();
        }

        public InventoryOperationResult Add(ItemDefinition definition, int quantity)
        {
            InventoryOperationResult result = Inventory.Add(definition, quantity);
            if (!result.Succeeded) return result;

            for (int i = 0; i < Inventory.Stacks.Count; i++)
            {
                ItemStack stack = Inventory.Stacks[i];
                if (stack.Definition.ItemId != definition.ItemId) continue;
                if (!IsAssignedToQuickBar(stack.StackId)) QuickBar.AssignFirstAvailable(stack.StackId);
                break;
            }

            return result;
        }

        private bool IsAssignedToQuickBar(string stackId)
        {
            for (int i = 0; i < QuickBar.StackIds.Count; i++)
                if (QuickBar.StackIds[i] == stackId) return true;
            return false;
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

        private void EnsureInitialized()
        {
            if (_inventory != null) return;
            _inventory = new Domain.Inventory(_totalCapacity);
            _quickBar = new QuickBarConfiguration(_inventory);
            if (_survival == null) _survival = GetComponent<PlayerSurvival>();
        }
    }
}
