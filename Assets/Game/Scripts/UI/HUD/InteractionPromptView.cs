using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    /// <summary>
    /// Shows what the player can do right now. Two sources feed it: the proximity focus that the E key
    /// acts on, and the structure under the cursor. The structure wins when both offer something, because
    /// pointing at one is the more deliberate of the two.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        private const string ReachInput = "E";
        private const string PointerInput = "right-click";

        private PlayerInteractor _interactor;
        private BuildingInteractionController _buildings;
        private InventoryView _inventoryView;
        private string _reachPrompt = string.Empty;
        private string _buildingPrompt = string.Empty;

        private string Prompt => string.IsNullOrWhiteSpace(_buildingPrompt) ? _reachPrompt : _buildingPrompt;
        private string InputHint => string.IsNullOrWhiteSpace(_buildingPrompt) ? ReachInput : PointerInput;

        public void Bind(PlayerInteractor interactor)
        {
            UnsubscribeInteractor();
            _interactor = interactor;
            if (_interactor == null)
            {
                Debug.LogError($"{nameof(InteractionPromptView)} requires a player interactor.", this);
                enabled = false;
                return;
            }

            _interactor.PromptChanged += HandleReachPromptChanged;
            HandleReachPromptChanged(_interactor.CurrentPrompt);
        }

        /// <summary>Optional: only the surface has placed structures to point at.</summary>
        public void Bind(BuildingInteractionController buildings)
        {
            UnsubscribeBuildings();
            _buildings = buildings;
            if (_buildings == null)
            {
                return;
            }

            _buildings.PromptChanged += HandleBuildingPromptChanged;
            HandleBuildingPromptChanged(_buildings.CurrentPrompt);
        }

        private void OnDisable()
        {
            _inventoryView?.SetInteractionPrompt(string.Empty);
        }

        private void OnDestroy()
        {
            _inventoryView?.SetInteractionPrompt(string.Empty);
            UnsubscribeInteractor();
            UnsubscribeBuildings();
        }

        private void LateUpdate()
        {
            // The quick bar is created after this view, so the hand-off is resolved lazily.
            if (_inventoryView == null)
            {
                _inventoryView = GetComponent<InventoryView>();
            }

            _inventoryView?.SetInteractionPrompt(Prompt, InputHint);
        }

        private void OnGUI()
        {
            // The quick bar renders the prompt whenever it is available.
            if (_inventoryView != null || string.IsNullOrWhiteSpace(Prompt))
            {
                return;
            }

            const float width = 320f;
            const float height = 44f;
            float left = Mathf.Max(8f, (Screen.width - width) * 0.5f);
            float top = Mathf.Max(8f, Screen.height - height - 48f);
            GUI.Box(new Rect(left, top, Mathf.Min(width, Screen.width - 16f), height),
                $"Press {InputHint} to {Prompt}");
        }

        private void HandleReachPromptChanged(string prompt)
        {
            _reachPrompt = prompt ?? string.Empty;
        }

        private void HandleBuildingPromptChanged(string prompt)
        {
            _buildingPrompt = prompt ?? string.Empty;
        }

        private void UnsubscribeInteractor()
        {
            if (_interactor != null)
            {
                _interactor.PromptChanged -= HandleReachPromptChanged;
            }
        }

        private void UnsubscribeBuildings()
        {
            if (_buildings != null)
            {
                _buildings.PromptChanged -= HandleBuildingPromptChanged;
            }
        }
    }
}
