using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Storage;
using UnityEngine;

namespace PlanetSurvival.Storage.Runtime
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class StorageContainer : MonoBehaviour, IInteractable
    {
        private InventoryModel _inventory;
        private StorageView _view;
        private string _displayName;

        public string Prompt => $"open {_displayName}";
        public InventoryModel Inventory => _inventory;

        public void Bind(string displayName, InventoryModel inventory, StorageView view)
        {
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "storage" : displayName;
            _inventory = inventory;
            _view = view;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Actor != null && context.Inventory != null && _inventory != null && _view != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _displayName,
                _inventory,
                context.Inventory,
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }
    }
}
