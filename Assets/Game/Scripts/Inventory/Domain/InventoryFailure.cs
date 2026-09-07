namespace PlanetSurvival.Inventory.Domain
{
    public enum InventoryFailure
    {
        None,
        InvalidItem,
        InvalidQuantity,
        InsufficientCapacity,
        InsufficientSlots,
        StackNotFound,
        InsufficientQuantity,
        ItemCannotBeUsed,
        EffectRejected
    }
}
