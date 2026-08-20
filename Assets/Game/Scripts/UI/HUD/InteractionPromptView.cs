using PlanetSurvival.Player.Interaction;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        private PlayerInteractor _interactor;
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

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(_prompt))
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
