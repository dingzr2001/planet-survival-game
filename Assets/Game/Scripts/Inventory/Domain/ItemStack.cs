using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Inventory.Domain
{
    public sealed class ItemStack
    {
        internal ItemStack(string stackId, ItemDefinition definition, int quantity)
        {
            StackId = stackId;
            Definition = definition;
            Quantity = quantity;
        }

        public string StackId { get; }
        public ItemDefinition Definition { get; }
        public int Quantity { get; private set; }

        internal void Add(int quantity)
        {
            Quantity += quantity;
        }

        internal void Remove(int quantity)
        {
            Quantity -= quantity;
        }
    }
}
