using PlanetSurvival.Core.Flow;
using UnityEngine;

namespace PlanetSurvival.UI.Menu
{
    [DisallowMultipleComponent]
    public sealed class GameOverView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 210f;

        private GameFlowController _flowController;

        private void OnEnable()
        {
            _flowController = FindFirstObjectByType<GameFlowController>();
            if (_flowController == null)
            {
                Debug.LogError($"{nameof(GameOverView)} requires a persistent {nameof(GameFlowController)}.", this);
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
            if (_flowController.State != GameFlowState.GameOver)
            {
                return;
            }

            float left = (Screen.width - PanelWidth) * 0.5f;
            float top = (Screen.height - PanelHeight) * 0.5f;
            GUILayout.BeginArea(new Rect(left, top, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("YOU DIED", GUI.skin.box);
            GUILayout.Space(20f);

            if (GUILayout.Button("Restart", GUILayout.Height(48f)))
            {
                _flowController.RestartGame();
            }

            if (GUILayout.Button("Main Menu", GUILayout.Height(48f)))
            {
                _flowController.ReturnToMainMenu();
            }

            GUILayout.EndArea();
        }

        private void HandleStateChanged(GameFlowState state)
        {
            enabled = state == GameFlowState.Playing || state == GameFlowState.GameOver;
        }
    }
}
