using System;
using System.Collections.Generic;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// The single way a placed structure is operated: hover it to read its prompt, right-click it to open
    /// its panel. Proximity focus deliberately ignores buildings, so standing between two machines can no
    /// longer put the wrong one under the E key — the structure under the cursor is the one that answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingInteractionController : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _components = new(8);

        private PlayerInteractor _interactor;
        private PlayerSurvival _survival;
        private PlayerInventory _inventory;
        private BuildingPlacementController _placement;
        private Camera _camera;
        private string _lastPrompt = string.Empty;

        /// <summary>What the hovered structure offers, or empty when the cursor is over none.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        public event Action<string> PromptChanged;

        public void Bind(GameObject player, BuildingPlacementController placement, Camera targetCamera)
        {
            _placement = placement;
            _camera = targetCamera;
            if (player == null)
            {
                Debug.LogError($"{nameof(BuildingInteractionController)} requires the player.", this);
                enabled = false;
                return;
            }

            _interactor = player.GetComponent<PlayerInteractor>();
            _survival = player.GetComponent<PlayerSurvival>();
            _inventory = player.GetComponent<PlayerInventory>();
            if (_interactor == null || _inventory == null)
            {
                Debug.LogError(
                    $"{nameof(BuildingInteractionController)} requires a {nameof(PlayerInteractor)} and backpack " +
                    "on the player.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_interactor == null)
            {
                return;
            }

            IInteractable hovered = ResolveHovered();
            SetPrompt(hovered == null ? string.Empty : hovered.Prompt);
            // The placement controller consumes the right click that leaves build mode, so a structure
            // under the cursor must not also open on that same click.
            if (hovered == null || !Input.GetMouseButtonDown(1) ||
                (_placement != null && (_placement.IsPlacing || _placement.CancelledPlacementThisFrame)))
            {
                return;
            }

            hovered.Interact(BuildContext());
        }

        /// <summary>Opens whatever structure is under the given screen point. Exposed for tests.</summary>
        public bool TryInteractAt(Vector2 screenPosition)
        {
            IInteractable hovered = ResolveAt(screenPosition);
            if (hovered == null)
            {
                return false;
            }

            hovered.Interact(BuildContext());
            return true;
        }

        private IInteractable ResolveHovered() => ResolveAt(Input.mousePosition);

        private IInteractable ResolveAt(Vector2 screenPosition)
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            if (_interactor == null || !BuildingPointer.TryPick(
                    _camera, screenPosition, _interactor.transform, _interactor.InteractionRadius,
                    out BuildSiteView building))
            {
                return null;
            }

            // A structure carries its construction-site interaction and, once finished, its machine
            // interaction. A machine station stays disabled until the structure stands, so skipping
            // disabled components is what keeps a half-built shed answering with "cancel" and nothing else.
            InteractionContext context = BuildContext();
            building.GetComponents(_components);
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].isActiveAndEnabled && _components[i] is IInteractable candidate &&
                    candidate.CanInteract(context))
                {
                    return candidate;
                }
            }

            return null;
        }

        private InteractionContext BuildContext() =>
            new(_interactor.gameObject, _survival, _inventory);

        private void SetPrompt(string prompt)
        {
            string resolved = prompt ?? string.Empty;
            if (resolved == _lastPrompt)
            {
                return;
            }

            _lastPrompt = resolved;
            CurrentPrompt = resolved;
            PromptChanged?.Invoke(resolved);
        }
    }
}
