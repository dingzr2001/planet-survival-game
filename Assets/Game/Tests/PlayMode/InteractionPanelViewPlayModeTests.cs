using System.Collections;
using NUnit.Framework;
using PlanetSurvival.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlanetSurvival.Tests
{
    /// <summary>
    /// The base panel's Unity lifecycle. The Escape handling every panel relies on lives in a single
    /// <c>Update</c> declared on the base class, so these tests prove Unity actually drives it on a derived
    /// panel and that a panel is released exactly once however it closes.
    /// </summary>
    public sealed class InteractionPanelViewPlayModeTests
    {
        private sealed class ProbePanel : InteractionPanelView
        {
            public int OpenUpdates { get; private set; }
            public int Closes { get; private set; }

            protected override InteractionPanelTheme Theme => InteractionPanelTheme.Logistics;

            public void OpenWithoutPlayer() => BeginSession(null, null);

            protected override void OnClosed() => Closes++;

            protected override void OnOpenUpdate() => OpenUpdates++;
        }

        [UnityTest]
        public IEnumerator OpenPanel_IsDrivenByTheBaseClassUpdate()
        {
            var hud = new GameObject("HUD");
            ProbePanel panel = hud.AddComponent<ProbePanel>();
            panel.OpenWithoutPlayer();

            yield return null;
            yield return null;

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(panel.OpenUpdates, Is.GreaterThan(0),
                "Escape handling lives in the base Update; without it no panel closes on Escape.");
            Object.Destroy(hud);
        }

        [UnityTest]
        public IEnumerator ClosedPanel_StopsReceivingUpdates()
        {
            var hud = new GameObject("HUD");
            ProbePanel panel = hud.AddComponent<ProbePanel>();
            panel.OpenWithoutPlayer();
            yield return null;
            panel.Close();
            int updatesAtClose = panel.OpenUpdates;

            yield return null;
            yield return null;

            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.OpenUpdates, Is.EqualTo(updatesAtClose));
            Object.Destroy(hud);
        }

        [UnityTest]
        public IEnumerator DisablingTheComponent_ClosesThePanelOnce()
        {
            var hud = new GameObject("HUD");
            ProbePanel panel = hud.AddComponent<ProbePanel>();
            panel.OpenWithoutPlayer();
            yield return null;

            panel.enabled = false;

            Assert.That(panel.IsOpen, Is.False,
                "A disabled panel stops drawing, so it must also hand the player's controls back.");
            Assert.That(panel.Closes, Is.EqualTo(1));
            Object.Destroy(hud);
            yield return null;
        }
    }
}
