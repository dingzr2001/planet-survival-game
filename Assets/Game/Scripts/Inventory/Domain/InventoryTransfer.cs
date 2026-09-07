using System;

namespace PlanetSurvival.Inventory.Domain
{
    public static class InventoryTransfer
    {
        public static InventoryOperationResult Transfer(
            Inventory source,
            Inventory destination,
            string stackId,
            int quantity)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (ReferenceEquals(source, destination))
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidItem,
                    "Source and destination inventory must be different.");
            }

            if (quantity <= 0)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidQuantity,
                    "Quantity must be positive.");
            }

            ItemStack stack = source.FindStack(stackId);
            if (stack == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.StackNotFound,
                    $"Stack '{stackId}' was not found in the source inventory.");
            }

            if (quantity > stack.Quantity)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InsufficientQuantity,
                    $"Stack '{stackId}' contains only {stack.Quantity} items.");
            }

            InventoryOperationResult destinationCheck = destination.CanAdd(stack.Definition, quantity);
            if (!destinationCheck.Succeeded)
            {
                return destinationCheck;
            }

            // Both operations are fully validated before either collection is changed.
            InventoryOperationResult removeResult = source.Remove(stackId, quantity);
            if (!removeResult.Succeeded)
            {
                return removeResult;
            }

            return destination.Add(stack.Definition, quantity);
        }
    }
}
