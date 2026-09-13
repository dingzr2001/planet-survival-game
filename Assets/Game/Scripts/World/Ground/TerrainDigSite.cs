using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Player.Animation;
using PlanetSurvival.Player.Interaction;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The interaction on one diggable terrain tile. It owns no terrain state of its own: what the tile is
    /// and how much of it is left both come from the <see cref="TerrainTileMap"/>, so a site can be
    /// created and thrown away as the player walks without losing dig progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainDigSite : MonoBehaviour, IInteractable
    {
        private TerrainTileMap _map;
        private TerrainTileCoordinate _tile;
        private Collider _interactionCollider;
        private GameObject _digger;
        private PlayerInventory _inventory;
        private PlayerAnimationController _digAnimation;
        private string _animatingToolItemId = string.Empty;
        private float _remainingTime;

        private Collider InteractionCollider
        {
            get
            {
                if (_interactionCollider == null)
                {
                    _interactionCollider = GetComponent<Collider>();
                }

                return _interactionCollider;
            }
        }

        public TerrainTileCoordinate Tile => _tile;
        public bool IsDigging => _digger != null;

        /// <summary>The terrain still covering this tile, or null once it has been dug away.</summary>
        public TerrainSurfaceDefinition Surface => _map?.GetSurface(_tile);

        public string Prompt
        {
            get
            {
                TerrainSurfaceDefinition surface = Surface;
                if (surface == null)
                {
                    return string.Empty;
                }

                if (IsDigging)
                {
                    return $"Digging {surface.DisplayName} ({_remainingTime:0.0}s)";
                }

                int remaining = _map.GetRemainingDigs(_tile);
                if (!string.IsNullOrEmpty(surface.RequiredToolItemId))
                {
                    return $"Dig {surface.DisplayName} ({remaining} left, requires {surface.RequiredToolItemId})";
                }

                return $"Dig {surface.DisplayName} ({remaining} left)";
            }
        }

        public void Configure(TerrainTileMap map, TerrainTileCoordinate tile)
        {
            CancelDigging();
            _map = map;
            _tile = tile;
        }

        private void Awake() => _interactionCollider = GetComponent<Collider>();

        public bool CanInteract(in InteractionContext context)
        {
            if (_map == null || context.Actor == null || context.Inventory == null || Surface == null)
            {
                return false;
            }

            return !IsDigging || context.Actor == _digger;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            if (IsDigging)
            {
                CancelDigging();
                return;
            }

            TerrainSurfaceDefinition surface = Surface;
            if (!surface.IsValid(out string error))
            {
                Debug.LogError($"Cannot dig terrain at {_tile}: {error}", this);
                return;
            }

            if (!HasRequiredTool(context.Inventory, surface.RequiredToolItemId))
            {
                Debug.LogWarning(
                    $"Digging '{surface.TerrainId}' requires item '{surface.RequiredToolItemId}'.", this);
                return;
            }

            _digger = context.Actor;
            _inventory = context.Inventory;
            _remainingTime = surface.DigDuration;
            StartDiggingAnimation(surface);
        }

        public void Advance(float elapsedSeconds)
        {
            if (!IsDigging || elapsedSeconds <= 0f)
            {
                return;
            }

            TerrainSurfaceDefinition surface = Surface;
            if (_digger == null || surface == null || GetDistanceToDigger() > surface.DigDistance)
            {
                CancelDigging();
                return;
            }

            _remainingTime -= elapsedSeconds;
            if (_remainingTime <= 0f)
            {
                CompleteDig(surface);
            }
        }

        public void CancelDigging()
        {
            StopDiggingAnimation();
            _digger = null;
            _inventory = null;
            _remainingTime = 0f;
        }

        private void Update() => Advance(Time.deltaTime);

        private void OnDisable()
        {
            if (IsDigging)
            {
                CancelDigging();
            }
        }

        private float GetDistanceToDigger()
        {
            Vector3 diggerPosition = _digger.transform.position;
            Vector3 closestPoint = InteractionCollider != null
                ? InteractionCollider.ClosestPoint(diggerPosition)
                : transform.position;
            return Vector3.Distance(diggerPosition, closestPoint);
        }

        private static bool HasRequiredTool(PlayerInventory inventory, string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
            {
                return true;
            }

            for (int i = 0; i < inventory.Inventory.Stacks.Count; i++)
            {
                if (inventory.Inventory.Stacks[i].Definition.ItemId == toolId)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartDiggingAnimation(TerrainSurfaceDefinition surface)
        {
            if (_digger == null || string.IsNullOrEmpty(surface.RequiredToolItemId))
            {
                return;
            }

            _digAnimation = _digger.GetComponentInChildren<PlayerAnimationController>();
            if (_digAnimation == null)
            {
                return;
            }

            // Remembered separately: the tile can fall back to base regolith mid-swing, and the animation
            // still has to be stopped with the tool it was started with.
            _animatingToolItemId = surface.RequiredToolItemId;
            _digAnimation.TryPlayToolAction(_animatingToolItemId);
        }

        private void StopDiggingAnimation()
        {
            if (_digAnimation == null)
            {
                return;
            }

            _digAnimation.StopToolAction(_animatingToolItemId);
            _digAnimation = null;
            _animatingToolItemId = string.Empty;
        }

        private void CompleteDig(TerrainSurfaceDefinition surface)
        {
            if (!TryGrantYields(surface))
            {
                CancelDigging();
                return;
            }

            CancelDigging();
            // The map raises its change event from here, which may retire this site: nothing may touch
            // the component after the dig is applied.
            _map.Dig(_tile);
        }

        private bool TryGrantYields(TerrainSurfaceDefinition surface)
        {
            // Check every output first so a failed dig never partially changes the inventory.
            int requiredCapacity = 0;
            for (int i = 0; i < surface.Yields.Count; i++)
            {
                ResourceYield yield = surface.Yields[i];
                InventoryOperationResult validation = _inventory.Inventory.CanAdd(yield.Item, yield.Quantity);
                if (!validation.Succeeded && validation.Failure != InventoryFailure.InsufficientCapacity)
                {
                    Debug.LogError($"Terrain '{surface.TerrainId}' has an invalid yield: {validation.Message}", this);
                    return false;
                }

                requiredCapacity += yield.Item.CapacityPerItem * yield.Quantity;
            }

            if (requiredCapacity > _inventory.Inventory.RemainingCapacity)
            {
                Debug.LogWarning(
                    $"Could not dig '{surface.TerrainId}': inventory needs {requiredCapacity} free capacity.", this);
                return false;
            }

            for (int i = 0; i < surface.Yields.Count; i++)
            {
                ResourceYield yield = surface.Yields[i];
                InventoryOperationResult result = _inventory.Add(yield.Item, yield.Quantity);
                if (!result.Succeeded)
                {
                    Debug.LogError($"Validated terrain yield failed unexpectedly: {result.Message}", this);
                    return false;
                }
            }

            return true;
        }
    }
}
