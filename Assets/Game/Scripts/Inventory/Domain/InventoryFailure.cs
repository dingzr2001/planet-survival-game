namespace PlanetSurvival.Inventory.Domain
{
    public enum InventoryFailure
    {
        None,
        InvalidItem,
        InvalidQuantity,
        InsufficientCapacity,
        StackNotFound,
        InsufficientQuantity,
        ItemCannotBeUsed,
        EffectRejected
    }
}
