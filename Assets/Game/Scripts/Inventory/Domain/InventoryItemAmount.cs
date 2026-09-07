using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Inventory.Domain
{
    public readonly struct InventoryItemAmount
    {
        public InventoryItemAmount(ItemDefinition definition, int quantity)
        {
            Definition = definition;
            Quantity = quantity;
        }

        public ItemDefinition Definition { get; }
        public int Quantity { get; }
    }
}
