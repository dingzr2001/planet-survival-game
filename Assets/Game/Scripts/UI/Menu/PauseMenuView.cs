using PlanetSurvival.Core.Flow;
using UnityEngine;

namespace PlanetSurvival.UI.Menu
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuView : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float PanelHeight = 310f;

        private GameFlowController _flowController;
        private bool _showSettings;

        private void OnEnable()
        {
            _flowController = FindFirstObjectByType<GameFlowController>();
            if (_flowController == null)
            {
                Debug.LogError($"{nameof(PauseMenuView)} requires a persistent {nameof(GameFlowController)}.", this);
                enabled = false;
                return;
            }

            _flowController.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_flowController != null)
            {
                _flowController.StateChanged -= HandleStateChanged;
            }
        }

        private void OnGUI()
        {
            if (_flowController.State != GameFlowState.Paused)
            {
                return;
            }

            float left = (Screen.width - PanelWidth) * 0.5f;
            float top = (Screen.height - PanelHeight) * 0.5f;
            GUILayout.BeginArea(new Rect(left, top, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("Paused", GUI.skin.box);
            GUILayout.Space(16f);

            if (GUILayout.Button("Resume", GUILayout.Height(42f)))
            {
                _flowController.Resume();
            }

            if (GUILayout.Button("Restart", GUILayout.Height(42f)))
            {
                _flowController.RestartGame();
            }

            if (GUILayout.Button("Settings", GUILayout.Height(42f)))
            {
                _showSettings = !_showSettings;
            }

            if (_showSettings)
            {
                GUILayout.Label("Audio and display settings are coming soon.");
            }

            if (GUILayout.Button("Main Menu", GUILayout.Height(42f)))
            {
                _flowController.ReturnToMainMenu();
            }

            GUILayout.EndArea();
        }

        private void HandleStateChanged(GameFlowState state)
        {
            enabled = state == GameFlowState.Playing || state == GameFlowState.Paused;
        }
    }
}
