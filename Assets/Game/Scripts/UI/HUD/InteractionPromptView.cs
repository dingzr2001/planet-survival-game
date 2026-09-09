using PlanetSurvival.Player.Interaction;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        private PlayerInteractor _interactor;
        private InventoryView _inventoryView;
        private string _prompt = string.Empty;

        public void Bind(PlayerInteractor interactor)
        {
            Unsubscribe();
            _interactor = interactor;
            if (_interactor == null)
            {
                Debug.LogError($"{nameof(InteractionPromptView)} requires a player interactor.", this);
                enabled = false;
                return;
            }

            _interactor.PromptChanged += HandlePromptChanged;
            HandlePromptChanged(_interactor.CurrentPrompt);
        }

        private void OnDisable()
        {
            _inventoryView?.SetInteractionPrompt(string.Empty);
        }

        private void OnDestroy()
        {
            _inventoryView?.SetInteractionPrompt(string.Empty);
            Unsubscribe();
        }

        private void LateUpdate()
        {
            // The quick bar is created after this view, so the hand-off is resolved lazily.
            if (_inventoryView == null)
            {
                _inventoryView = GetComponent<InventoryView>();
            }

            _inventoryView?.SetInteractionPrompt(_prompt);
        }

        private void OnGUI()
        {
            // The quick bar renders the prompt whenever it is available.
            if (_inventoryView != null || string.IsNullOrWhiteSpace(_prompt))
            {
                return;
            }

            const float width = 320f;
            const float height = 44f;
            float left = Mathf.Max(8f, (Screen.width - width) * 0.5f);
            float top = Mathf.Max(8f, Screen.height - height - 48f);
            GUI.Box(new Rect(left, top, Mathf.Min(width, Screen.width - 16f), height), $"Press E to {_prompt}");
        }

        private void HandlePromptChanged(string prompt)
        {
            _prompt = prompt ?? string.Empty;
        }

        private void Unsubscribe()
        {
            if (_interactor != null)
            {
                _interactor.PromptChanged -= HandlePromptChanged;
            }
        }
    }
}
