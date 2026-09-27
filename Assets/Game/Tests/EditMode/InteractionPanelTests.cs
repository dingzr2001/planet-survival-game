using NUnit.Framework;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    /// <summary>
    /// Covers the bookkeeping every interaction panel shares: the controls a panel silences while it is
    /// open, and the record the pause menu reads to tell whether Escape was already spoken for.
    /// </summary>
    public sealed class InteractionPanelTests
    {
        private GameObject _player;
        private GameObject _hud;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player", typeof(CharacterController), typeof(PlanarPlayerMotor),
                typeof(PlayerInteractor));
            _hud = new GameObject("HUD", typeof(InventoryView));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_hud);
        }

        [Test]
        public void Capture_DisablesTheControlsAPanelCoversUp()
        {
            var lockedControls = new PanelControlLock();

            lockedControls.Capture(_player.GetComponent<PlanarPlayerMotor>(),
                _player.GetComponent<PlayerInteractor>(), _hud.GetComponent<InventoryView>());

            Assert.That(_player.GetComponent<PlanarPlayerMotor>().enabled, Is.False);
            Assert.That(_player.GetComponent<PlayerInteractor>().enabled, Is.False);
            Assert.That(_hud.GetComponent<InventoryView>().enabled, Is.False,
                "The backpack must not stay open behind a full-screen panel.");
        }

        [Test]
        public void Release_RestoresWhatEachControlWasBeforeTheCapture()
        {
            var lockedControls = new PanelControlLock();
            PlayerInteractor interactor = _player.GetComponent<PlayerInteractor>();
            interactor.enabled = false;

            lockedControls.Capture(_player.GetComponent<PlanarPlayerMotor>(), interactor,
                _hud.GetComponent<InventoryView>());
            lockedControls.Release();

            Assert.That(_player.GetComponent<PlanarPlayerMotor>().enabled, Is.True);
            Assert.That(interactor.enabled, Is.False, "A control disabled beforehand must stay disabled.");
            Assert.That(_hud.GetComponent<InventoryView>().enabled, Is.True);
        }

        [Test]
        public void Capture_Twice_StillRestoresTheOriginalStates()
        {
            var lockedControls = new PanelControlLock();
            PlanarPlayerMotor motor = _player.GetComponent<PlanarPlayerMotor>();

            // Opening a panel over itself must not record the already-disabled state as the one to restore.
            lockedControls.Capture(motor, null, null);
            lockedControls.Capture(motor, null, null);
            lockedControls.Release();

            Assert.That(motor.enabled, Is.True);
        }

        [Test]
        public void Release_WithoutCapture_DoesNothing()
        {
            var lockedControls = new PanelControlLock();

            Assert.DoesNotThrow(() => lockedControls.Release());
        }

        [Test]
        public void Registry_BlocksGlobalEscape_WhileAnyPanelIsOpen()
        {
            ModalPanelRegistry registry = _hud.AddComponent<ModalPanelRegistry>();
            Transform firstPanel = _hud.transform;
            Transform secondPanel = _player.transform;

            Assert.That(registry.BlocksGlobalEscape, Is.False);

            registry.SetOpen(firstPanel, true);
            registry.SetOpen(secondPanel, true);
            Assert.That(registry.BlocksGlobalEscape, Is.True);

            registry.SetOpen(firstPanel, false);
            Assert.That(registry.BlocksGlobalEscape, Is.True, "The second panel is still open.");
        }

        [Test]
        public void Registry_KeepsBlockingForTheRestOfTheFrameAPanelClosedOn()
        {
            ModalPanelRegistry registry = _hud.AddComponent<ModalPanelRegistry>();
            Transform panel = _hud.transform;

            registry.SetOpen(panel, true);
            registry.SetOpen(panel, false);

            // Update order between a panel and the pause menu is undefined, so the answer has to survive
            // the panel closing first.
            Assert.That(registry.BlocksGlobalEscape, Is.True);
        }

        [Test]
        public void Registry_IgnoresAPanelItNeverSawOpen()
        {
            ModalPanelRegistry registry = _hud.AddComponent<ModalPanelRegistry>();

            registry.SetOpen(_hud.transform, false);
            registry.SetOpen(null, true);

            Assert.That(registry.BlocksGlobalEscape, Is.False);
        }
    }
}
