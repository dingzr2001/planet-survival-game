using PlanetSurvival.Core.Flow;
using UnityEngine;

namespace PlanetSurvival.UI.Menu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 180f;

        private GameFlowController _flowController;

        private void Start()
        {
            _flowController = FindFirstObjectByType<GameFlowController>();
            if (_flowController == null)
            {
                Debug.LogError($"{nameof(MainMenuView)} requires a persistent {nameof(GameFlowController)}.", this);
                enabled = false;
            }
        }

        private void OnGUI()
        {
            float left = (Screen.width - PanelWidth) * 0.5f;
            float top = (Screen.height - PanelHeight) * 0.5f;
            GUILayout.BeginArea(new Rect(left, top, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("PLANET SURVIVAL", GUI.skin.box);
            GUILayout.Space(24f);

            if (GUILayout.Button("Start Game", GUILayout.Height(48f)))
            {
                enabled = false;
                _flowController.StartGame();
            }

            GUILayout.EndArea();
        }
    }
}
