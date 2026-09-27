using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// What every full-screen interaction panel does the same way: hold the player's controls while it is
    /// open, close on Escape, tell the HUD it is open so the pause menu stays out of the way, and hand the
    /// controls back exactly once however it is closed — button, Escape, disable or destroy.
    /// </summary>
    /// <remarks>
    /// A panel supplies its own <see cref="Open"/> overload with whatever the machine needs, ends it with
    /// <see cref="BeginSession(GameObject)"/>, and drops its references in <see cref="OnClosed"/>.
    /// </remarks>
    public abstract class InteractionPanelView : MonoBehaviour
    {
        private readonly PanelControlLock _controls = new();
        private ModalPanelRegistry _registry;
        private InteractionPanelStyles _styles;

        /// <summary>True between a successful <c>Open</c> and the next close.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The colours this panel draws with; machine families differ, the structure does not.</summary>
        protected abstract InteractionPanelTheme Theme { get; }

        /// <summary>
        /// Text styles for <see cref="Theme"/>, built on first use. Only valid inside <c>OnGUI</c>, because
        /// the styles derive from <see cref="GUI.skin"/>.
        /// </summary>
        protected InteractionPanelStyles Styles => _styles ??= InteractionPanelStyles.Create(Theme);

        /// <summary>Takes the controls found on the interacting player and marks the panel open.</summary>
        protected void BeginSession(GameObject player)
        {
            BeginSession(
                player == null ? null : player.GetComponent<PlanarPlayerMotor>(),
                player == null ? null : player.GetComponent<PlayerInteractor>());
        }

        /// <summary>Takes the controls a caller already resolved and marks the panel open.</summary>
        protected void BeginSession(PlanarPlayerMotor motor, PlayerInteractor interactor)
        {
            _controls.Capture(motor, interactor, GetComponent<InventoryView>());
            _registry ??= GetComponent<ModalPanelRegistry>();
            _registry?.SetOpen(this, true);
            IsOpen = true;
        }

        /// <summary>Closes the panel and restores the player's controls. Safe to call when already closed.</summary>
        public void Close()
        {
            bool wasOpen = IsOpen;
            IsOpen = false;
            _controls.Release();
            if (wasOpen)
            {
                _registry?.SetOpen(this, false);
            }

            OnClosed();
        }

        /// <summary>Drops whatever the panel was bound to, after the controls have been handed back.</summary>
        protected abstract void OnClosed();

        /// <summary>Per-frame work a panel needs while it is open, such as driving an effect.</summary>
        protected virtual void OnOpenUpdate()
        {
        }

        protected virtual void OnDisable() => Close();

        protected virtual void OnDestroy() => Close();

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            OnOpenUpdate();
        }
    }
}
