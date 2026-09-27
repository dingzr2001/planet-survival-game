using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// Tracks which full-screen panels on this HUD are open, so the pause menu does not also answer the
    /// Escape press that closed one.
    /// </summary>
    /// <remarks>
    /// Update order between a panel and the pause menu is undefined, so a panel that closes on Escape would
    /// otherwise be gone by the time the pause menu looks. Keeping the frame a panel last closed on makes
    /// the answer the same whichever component runs first.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ModalPanelRegistry : MonoBehaviour
    {
        private readonly HashSet<Component> _openPanels = new();
        private int _lastCloseFrame = -1;

        /// <summary>True while any panel is open, and for the remainder of the frame one closed on.</summary>
        public bool BlocksGlobalEscape => _openPanels.Count > 0 || _lastCloseFrame == Time.frameCount;

        /// <summary>Records that <paramref name="panel"/> opened or closed.</summary>
        public void SetOpen(Component panel, bool open)
        {
            if (panel == null)
            {
                return;
            }

            if (open)
            {
                _openPanels.Add(panel);
            }
            else if (_openPanels.Remove(panel))
            {
                _lastCloseFrame = Time.frameCount;
            }
        }
    }
}
