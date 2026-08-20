using System;
using System.Collections.Generic;
using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Inventory.Domain
{
    public sealed class Inventory
    {
        private readonly List<ItemStack> _stacks = new();

        public Inventory(int totalCapacity)
        {
            if (totalCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCapacity));
            }

            TotalCapacity = totalCapacity;
        }

        public int TotalCapacity { get; }
        public int UsedCapacity { get; private set; }
        public int RemainingCapacity => TotalCapacity - UsedCapacity;
        public IReadOnlyList<ItemStack> Stacks => _stacks;
        public event Action Changed;
        public event Action<string> StackRemoved;

        public InventoryOperationResult CanAdd(ItemDefinition definition, int quantity)
        {
            InventoryOperationResult validation = ValidateDefinitionAndQuantity(definition, quantity);
            if (!validation.Succeeded)
            {
                return validation;
            }

            long requiredCapacity = (long)definition.CapacityPerItem * quantity;
            if (requiredCapacity > RemainingCapacity)
            {
                return InventoryOperationResult.Fail(
                    InventoryFailure.InsufficientCapacity,
                    $"Adding {quantity} x '{definition.ItemId}' requires {requiredCapacity} capacity, but only {RemainingCapacity} remains.");
            }

            return InventoryOperationResult.Success();
        }

        public InventoryOperationResult Add(ItemDefinition definition, int quantity)
        {
            InventoryOperationResult result = CanAdd(definition, quantity);
            if (!result.Succeeded)
            {
                return result;
            }

            int remaining = quantity;
            for (int i = 0; i < _stacks.Count && remaining > 0; i++)
            {
                ItemStack stack = _stacks[i];
                if (stack.Definition.ItemId != definition.ItemId || stack.Quantity >= definition.MaximumStackSize)
                {
                    continue;
                }

                int added = Math.Min(remaining, definition.MaximumStackSize - stack.Quantity);
                stack.Add(added);
                remaining -= added;
            }

            while (remaining > 0)
            {
                int stackQuantity = Math.Min(remaining, definition.MaximumStackSize);
                _stacks.Add(new ItemStack(Guid.NewGuid().ToString("N"), definition, stackQuantity));
                remaining -= stackQuantity;
            }

            UsedCapacity += definition.CapacityPerItem * quantity;
            Changed?.Invoke();
            return InventoryOperationResult.Success();
        }

        public InventoryOperationResult Remove(string stackId, int quantity)
        {
            if (quantity <= 0)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidQuantity, "Quantity must be positive.");
            }

            ItemStack stack = FindStack(stackId);
            if (stack == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.StackNotFound, $"Stack '{stackId}' was not found.");
            }

            if (quantity > stack.Quantity)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InsufficientQuantity, $"Stack '{stackId}' contains only {stack.Quantity} items.");
            }

            stack.Remove(quantity);
            UsedCapacity -= stack.Definition.CapacityPerItem * quantity;
            if (stack.Quantity == 0)
            {
                _stacks.Remove(stack);
                StackRemoved?.Invoke(stack.StackId);
            }

            Changed?.Invoke();
            return InventoryOperationResult.Success();
        }

        public ItemStack FindStack(string stackId)
        {
            if (string.IsNullOrEmpty(stackId))
            {
                return null;
            }

            return _stacks.Find(stack => stack.StackId == stackId);
        }

        private static InventoryOperationResult ValidateDefinitionAndQuantity(ItemDefinition definition, int quantity)
        {
            if (definition == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidItem, "Item definition is missing.");
            }

            if (!definition.IsValid(out string error))
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidItem, error);
            }

            return quantity > 0
                ? InventoryOperationResult.Success()
                : InventoryOperationResult.Fail(InventoryFailure.InvalidQuantity, "Quantity must be positive.");
        }
    }
}
