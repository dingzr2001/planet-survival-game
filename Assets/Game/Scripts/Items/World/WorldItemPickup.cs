using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using UnityEngine;

namespace PlanetSurvival.Items.World
{
    [DisallowMultipleComponent]
    public sealed class WorldItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition _definition;
        [SerializeField, Min(1)] private int _quantity = 1;

        public string Prompt => _definition == null ? "Pick up item" : $"Pick up {_definition.DisplayName}";

        public void Configure(ItemDefinition definition, int quantity)
        {
            _definition = definition;
            _quantity = quantity;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Inventory != null && _definition != null && _quantity > 0;
        }

        public void Interact(in InteractionContext context)
        {
            InventoryOperationResult result = context.Inventory.Add(_definition, _quantity);
            if (result.Succeeded)
            {
                Destroy(gameObject);
                return;
            }

            Debug.LogWarning($"Could not pick up '{_definition.ItemId}': {result.Message}", this);
        }
    }
}
