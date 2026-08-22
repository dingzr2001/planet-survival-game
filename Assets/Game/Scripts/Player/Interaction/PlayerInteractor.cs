using System;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Player.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _interactionRadius = 2f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        private readonly Collider[] _results = new Collider[12];
        private PlayerSurvival _survival;
        private PlayerInventory _inventory;
        private IInteractable _focusedInteractable;
        private string _lastPrompt = string.Empty;

        public string CurrentPrompt => _focusedInteractable?.Prompt ?? string.Empty;
        public event Action<string> PromptChanged;

        private void Awake()
        {
            _survival = GetComponent<PlayerSurvival>();
            _inventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            RefreshFocusedInteractable();

            if (Input.GetKeyDown(KeyCode.E))
            {
                TryInteract();
            }
        }

        public bool TryInteract()
        {
            RefreshFocusedInteractable();
            if (_focusedInteractable == null)
            {
                return false;
            }

            var context = new InteractionContext(gameObject, _survival, _inventory);
            _focusedInteractable.Interact(context);
            return true;
        }

        public void RefreshFocusedInteractable()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _interactionRadius,
                _results,
                _interactionLayers,
                QueryTriggerInteraction.Collide);

            IInteractable closest = null;
            float closestDistance = float.MaxValue;
            var context = new InteractionContext(gameObject, _survival, _inventory);

            for (int i = 0; i < count; i++)
            {
                if (!_results[i].TryGetComponent(out IInteractable candidate) || !candidate.CanInteract(context))
                {
                    continue;
                }

                float distance = (_results[i].ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            if (ReferenceEquals(closest, _focusedInteractable))
            {
                NotifyPromptIfChanged();
                return;
            }

            _focusedInteractable = closest;
            NotifyPromptIfChanged();
        }

        private void NotifyPromptIfChanged()
        {
            string prompt = CurrentPrompt;
            if (prompt == _lastPrompt) return;
            _lastPrompt = prompt;
            PromptChanged?.Invoke(prompt);
        }
    }
}
