using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class LandingPodLocationView : MonoBehaviour
    {
        private string _deckName;
        private string _purpose;

        public void Configure(string deckName, string purpose)
        {
            _deckName = deckName;
            _purpose = purpose;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 260f, 64f), string.Empty);
            GUI.Label(new Rect(32f, 27f, 230f, 22f), _deckName);
            GUI.Label(new Rect(32f, 49f, 230f, 22f), _purpose);
        }
    }
}
