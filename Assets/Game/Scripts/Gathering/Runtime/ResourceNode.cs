using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Player.Interaction;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private ResourceNodeDefinition _definition;

        private GameObject _gatherer;
        private PlayerInventory _inventory;
        private Collider _interactionCollider;
        private float _remainingTime;
        private bool _depleted;

        private Collider InteractionCollider
        {
            get
            {
                if (_interactionCollider == null) _interactionCollider = GetComponent<Collider>();
                return _interactionCollider;
            }
        }

        public bool IsGathering => _gatherer != null;
        public bool IsDepleted => _depleted;
        public string Prompt
        {
            get
            {
                if (_definition == null) return "Invalid resource";
                if (IsGathering) return $"Gathering {_definition.DisplayName} ({_remainingTime:0.0}s)";
                if (string.IsNullOrEmpty(_definition.RequiredToolItemId)) return $"Gather {_definition.DisplayName}";
                return $"Gather {_definition.DisplayName} (requires {_definition.RequiredToolItemId})";
            }
        }

        public void Configure(ResourceNodeDefinition definition) => _definition = definition;

        private void Awake() => _interactionCollider = GetComponent<Collider>();

        public bool CanInteract(in InteractionContext context)
        {
            if (_depleted || _definition == null || context.Actor == null || context.Inventory == null) return false;
            return !IsGathering || context.Actor == _gatherer;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            if (IsGathering)
            {
                CancelGathering();
                return;
            }

            if (!_definition.IsValid(out string error))
            {
                Debug.LogError($"Cannot gather resource on '{name}': {error}", this);
                return;
            }

            if (!HasRequiredTool(context.Inventory))
            {
                Debug.LogWarning($"Gathering '{_definition.ResourceId}' requires item '{_definition.RequiredToolItemId}'.", this);
                return;
            }

            _gatherer = context.Actor;
            _inventory = context.Inventory;
            _remainingTime = _definition.GatherDuration;
        }

        public void Advance(float elapsedSeconds)
        {
            if (!IsGathering || elapsedSeconds <= 0f) return;
            if (_gatherer == null || GetDistanceToGatherer() > _definition.GatherDistance)
            {
                CancelGathering();
                return;
            }

            _remainingTime -= elapsedSeconds;
            if (_remainingTime <= 0f) CompleteGathering();
        }

        public void CancelGathering()
        {
            _gatherer = null;
            _inventory = null;
            _remainingTime = 0f;
        }

        private void Update() => Advance(Time.deltaTime);

        private float GetDistanceToGatherer()
        {
            Vector3 gathererPosition = _gatherer.transform.position;
            Vector3 closestPoint = InteractionCollider != null
                ? InteractionCollider.ClosestPoint(gathererPosition)
                : transform.position;
            return Vector3.Distance(gathererPosition, closestPoint);
        }

        private bool HasRequiredTool(PlayerInventory inventory)
        {
            string toolId = _definition.RequiredToolItemId;
            if (string.IsNullOrEmpty(toolId)) return true;
            for (int i = 0; i < inventory.Inventory.Stacks.Count; i++)
                if (inventory.Inventory.Stacks[i].Definition.ItemId == toolId) return true;
            return false;
        }

        private void CompleteGathering()
        {
            // Check every output first so a failed harvest never partially changes the inventory.
            int requiredCapacity = 0;
            for (int i = 0; i < _definition.Yields.Count; i++)
            {
                ResourceYield yield = _definition.Yields[i];
                InventoryOperationResult validation = _inventory.Inventory.CanAdd(yield.Item, yield.Quantity);
                if (!validation.Succeeded && validation.Failure != InventoryFailure.InsufficientCapacity)
                {
                    Debug.LogError($"Resource '{_definition.ResourceId}' has an invalid yield: {validation.Message}", this);
                    CancelGathering();
                    return;
                }
                requiredCapacity += yield.Item.CapacityPerItem * yield.Quantity;
            }

            if (requiredCapacity > _inventory.Inventory.RemainingCapacity)
            {
                Debug.LogWarning($"Could not gather '{_definition.ResourceId}': inventory needs {requiredCapacity} free capacity.", this);
                CancelGathering();
                return;
            }

            for (int i = 0; i < _definition.Yields.Count; i++)
            {
                ResourceYield yield = _definition.Yields[i];
                InventoryOperationResult result = _inventory.Add(yield.Item, yield.Quantity);
                if (!result.Succeeded)
                {
                    Debug.LogError($"Validated resource yield failed unexpectedly: {result.Message}", this);
                    CancelGathering();
                    return;
                }
            }

            _depleted = true;
            CancelGathering();
            SetDepletedPresentation();
        }

        private void SetDepletedPresentation()
        {
            if (InteractionCollider != null) InteractionCollider.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }
    }
}
