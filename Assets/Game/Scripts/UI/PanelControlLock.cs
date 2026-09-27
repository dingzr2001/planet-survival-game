using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// The player controls a full-screen panel silences while it is open, and the exact states to hand back
    /// when it closes. A panel owns one of these for its whole lifetime and calls
    /// <see cref="Capture(PlanarPlayerMotor, PlayerInteractor, InventoryView)"/> on open and
    /// <see cref="Release"/> on close, so no panel can leave the player frozen or let the backpack open on
    /// top of itself.
    /// </summary>
    public sealed class PanelControlLock
    {
        private PlanarPlayerMotor _motor;
        private PlayerInteractor _interactor;
        private InventoryView _inventoryView;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;

        /// <summary>
        /// Locks the controls found on <paramref name="player"/> plus the backpack panel that shares the HUD
        /// object with <paramref name="panel"/>.
        /// </summary>
        public void Capture(GameObject player, Component panel)
        {
            Capture(
                player == null ? null : player.GetComponent<PlanarPlayerMotor>(),
                player == null ? null : player.GetComponent<PlayerInteractor>(),
                panel == null ? null : panel.GetComponent<InventoryView>());
        }

        /// <summary>
        /// Locks the given controls. Any previous capture is released first, so reopening a panel over itself
        /// never captures an already-disabled component as the state to restore.
        /// </summary>
        public void Capture(PlanarPlayerMotor motor, PlayerInteractor interactor, InventoryView inventoryView)
        {
            Release();
            _motor = motor;
            _interactor = interactor;
            _inventoryView = inventoryView;
            _restoreMotor = _motor != null && _motor.enabled;
            _restoreInteractor = _interactor != null && _interactor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_motor != null) _motor.enabled = false;
            if (_interactor != null) _interactor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
        }

        /// <summary>Restores whatever was captured. Safe to call when nothing is held.</summary>
        public void Release()
        {
            if (_motor != null) _motor.enabled = _restoreMotor;
            if (_interactor != null) _interactor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _motor = null;
            _interactor = null;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
        }
    }
}
