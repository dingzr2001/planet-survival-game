using System;
using System.Collections.Generic;
using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Inventory.Domain
{
    public sealed class Inventory
    {
        private readonly List<ItemStack> _stacks = new();

        public Inventory(int totalCapacity, int totalSlots = int.MaxValue)
        {
            if (totalCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCapacity));
            }

            if (totalSlots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSlots));
            }

            TotalCapacity = totalCapacity;
            TotalSlots = totalSlots;
        }

        public int TotalCapacity { get; }
        public int TotalSlots { get; }
        public int UsedSlots => _stacks.Count;
        public int RemainingSlots => TotalSlots - UsedSlots;
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

            int requiredSlots = CalculateAdditionalSlots(definition, quantity);
            if (requiredSlots > RemainingSlots)
            {
                return InventoryOperationResult.Fail(
                    InventoryFailure.InsufficientSlots,
                    $"Adding {quantity} x '{definition.ItemId}' needs {requiredSlots} new slots, but only {RemainingSlots} remain.");
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

        public int GetQuantity(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            int quantity = 0;
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition.ItemId == itemId)
                {
                    quantity += _stacks[i].Quantity;
                }
            }

            return quantity;
        }

        public InventoryOperationResult CanApplyTransaction(
            IReadOnlyList<InventoryItemAmount> removals,
            IReadOnlyList<InventoryItemAmount> additions)
        {
            return TryStageTransaction(removals, additions, out _, out _);
        }

        public InventoryOperationResult ApplyTransaction(
            IReadOnlyList<InventoryItemAmount> removals,
            IReadOnlyList<InventoryItemAmount> additions)
        {
            InventoryOperationResult result = TryStageTransaction(
                removals, additions, out List<StagedStack> stagedStacks, out int usedCapacity);
            if (!result.Succeeded)
            {
                return result;
            }

            for (int i = stagedStacks.Count - 1; i >= 0; i--)
            {
                StagedStack staged = stagedStacks[i];
                if (staged.ExistingStack == null)
                {
                    continue;
                }

                int difference = staged.Quantity - staged.ExistingStack.Quantity;
                if (difference > 0)
                {
                    staged.ExistingStack.Add(difference);
                }
                else if (difference < 0)
                {
                    staged.ExistingStack.Remove(-difference);
                }

                if (staged.Quantity == 0)
                {
                    _stacks.Remove(staged.ExistingStack);
                    StackRemoved?.Invoke(staged.ExistingStack.StackId);
                }
            }

            for (int i = 0; i < stagedStacks.Count; i++)
            {
                StagedStack staged = stagedStacks[i];
                if (staged.ExistingStack == null && staged.Quantity > 0)
                {
                    _stacks.Add(new ItemStack(Guid.NewGuid().ToString("N"), staged.Definition, staged.Quantity));
                }
            }

            UsedCapacity = usedCapacity;
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

        public void Clear()
        {
            if (_stacks.Count == 0)
            {
                return;
            }

            for (int i = _stacks.Count - 1; i >= 0; i--)
            {
                StackRemoved?.Invoke(_stacks[i].StackId);
            }

            _stacks.Clear();
            UsedCapacity = 0;
            Changed?.Invoke();
        }

        private int CalculateAdditionalSlots(ItemDefinition definition, int quantity)
        {
            int remaining = quantity;
            for (int i = 0; i < _stacks.Count && remaining > 0; i++)
            {
                ItemStack stack = _stacks[i];
                if (stack.Definition.ItemId == definition.ItemId)
                {
                    remaining -= Math.Min(remaining, definition.MaximumStackSize - stack.Quantity);
                }
            }

            return remaining <= 0
                ? 0
                : (int)(((long)remaining + definition.MaximumStackSize - 1) / definition.MaximumStackSize);
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

        private InventoryOperationResult TryStageTransaction(
            IReadOnlyList<InventoryItemAmount> removals,
            IReadOnlyList<InventoryItemAmount> additions,
            out List<StagedStack> stagedStacks,
            out int usedCapacity)
        {
            stagedStacks = new List<StagedStack>(_stacks.Count);
            usedCapacity = UsedCapacity;
            for (int i = 0; i < _stacks.Count; i++)
            {
                stagedStacks.Add(new StagedStack(_stacks[i], _stacks[i].Definition, _stacks[i].Quantity));
            }

            InventoryOperationResult validation = ValidateAmounts(removals);
            if (!validation.Succeeded)
            {
                return validation;
            }

            validation = ValidateAmounts(additions);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (removals != null)
            {
                for (int amountIndex = 0; amountIndex < removals.Count; amountIndex++)
                {
                    InventoryItemAmount removal = removals[amountIndex];
                    int remaining = removal.Quantity;
                    for (int stackIndex = stagedStacks.Count - 1; stackIndex >= 0 && remaining > 0; stackIndex--)
                    {
                        StagedStack staged = stagedStacks[stackIndex];
                        if (staged.Definition.ItemId != removal.Definition.ItemId || staged.Quantity == 0)
                        {
                            continue;
                        }

                        int removed = Math.Min(remaining, staged.Quantity);
                        staged.Quantity -= removed;
                        remaining -= removed;
                    }

                    if (remaining > 0)
                    {
                        return InventoryOperationResult.Fail(
                            InventoryFailure.InsufficientQuantity,
                            $"Inventory does not contain {removal.Quantity} x '{removal.Definition.ItemId}'.");
                    }
                }
            }

            if (additions != null)
            {
                for (int amountIndex = 0; amountIndex < additions.Count; amountIndex++)
                {
                    InventoryItemAmount addition = additions[amountIndex];
                    int remaining = addition.Quantity;
                    for (int stackIndex = 0; stackIndex < stagedStacks.Count && remaining > 0; stackIndex++)
                    {
                        StagedStack staged = stagedStacks[stackIndex];
                        if (staged.Definition.ItemId != addition.Definition.ItemId ||
                            staged.Quantity == 0 || staged.Quantity >= addition.Definition.MaximumStackSize)
                        {
                            continue;
                        }

                        int added = Math.Min(remaining, addition.Definition.MaximumStackSize - staged.Quantity);
                        staged.Quantity += added;
                        remaining -= added;
                    }

                    while (remaining > 0)
                    {
                        int stackQuantity = Math.Min(remaining, addition.Definition.MaximumStackSize);
                        stagedStacks.Add(new StagedStack(null, addition.Definition, stackQuantity));
                        remaining -= stackQuantity;
                    }
                }
            }

            int occupiedSlots = 0;
            long calculatedCapacity = 0;
            for (int i = 0; i < stagedStacks.Count; i++)
            {
                StagedStack staged = stagedStacks[i];
                if (staged.Quantity == 0)
                {
                    continue;
                }

                occupiedSlots++;
                calculatedCapacity += (long)staged.Definition.CapacityPerItem * staged.Quantity;
            }

            if (calculatedCapacity > TotalCapacity)
            {
                return InventoryOperationResult.Fail(
                    InventoryFailure.InsufficientCapacity,
                    $"Transaction requires {calculatedCapacity} capacity, but inventory capacity is {TotalCapacity}.");
            }

            if (occupiedSlots > TotalSlots)
            {
                return InventoryOperationResult.Fail(
                    InventoryFailure.InsufficientSlots,
                    $"Transaction requires {occupiedSlots} slots, but inventory has {TotalSlots}.");
            }

            usedCapacity = (int)calculatedCapacity;
            return InventoryOperationResult.Success();
        }

        private static InventoryOperationResult ValidateAmounts(IReadOnlyList<InventoryItemAmount> amounts)
        {
            if (amounts == null)
            {
                return InventoryOperationResult.Success();
            }

            for (int i = 0; i < amounts.Count; i++)
            {
                InventoryOperationResult validation = ValidateDefinitionAndQuantity(
                    amounts[i].Definition, amounts[i].Quantity);
                if (!validation.Succeeded)
                {
                    return validation;
                }
            }

            return InventoryOperationResult.Success();
        }

        private sealed class StagedStack
        {
            public StagedStack(ItemStack existingStack, ItemDefinition definition, int quantity)
            {
                ExistingStack = existingStack;
                Definition = definition;
                Quantity = quantity;
            }

            public ItemStack ExistingStack { get; }
            public ItemDefinition Definition { get; }
            public int Quantity { get; set; }
        }
    }
}
