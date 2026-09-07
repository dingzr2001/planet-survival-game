using System.Collections;
using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Storage.Runtime;
using PlanetSurvival.UI.Storage;
using PlanetSurvival.World.Interiors;
using PlanetSurvival.World.Presentation;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PlanetSurvival.Tests
{
    public sealed class FormalGameFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_StartPauseResumeAndReturnToMenu_MaintainsSingleOwnedRuntime()
        {
            SceneManager.LoadScene(GameSceneNames.Bootstrap, LoadSceneMode.Single);
            yield return WaitForScene(GameSceneNames.MainMenu);

            GameFlowController flowController = Object.FindFirstObjectByType<GameFlowController>();
            Assert.That(flowController, Is.Not.Null);
            Assert.That(flowController.State, Is.EqualTo(GameFlowState.MainMenu));

            flowController.StartGame();
            yield return WaitForScene(GameSceneNames.LandingPodHabitat);
            yield return null;

            Assert.That(Object.FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameClock>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Camera gameCamera = Object.FindFirstObjectByType<Camera>();
            Assert.That(gameCamera.orthographic, Is.True);
            Assert.That(Object.FindFirstObjectByType<PlanarPlayerMotor>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<WorldSpriteView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<FixedInteriorBackdrop>(), Is.Not.Null);
            Assert.That(GameObject.Find("Dining Table"), Is.Not.Null);
            Assert.That(GameObject.Find("Habitat Water Dispenser"), Is.Not.Null);
            StorageContainer refrigerator = Object.FindFirstObjectByType<StorageContainer>();
            Assert.That(refrigerator, Is.Not.Null);
            Assert.That(refrigerator.gameObject.name, Is.EqualTo("Habitat Refrigerator"));
            Assert.That(refrigerator.Inventory.TotalSlots, Is.EqualTo(12));
            Assert.That(Object.FindFirstObjectByType<StorageView>(), Is.Not.Null);
            PlayerWaterBottle waterBottle = Object.FindFirstObjectByType<PlayerWaterBottle>();
            Assert.That(waterBottle, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlanetSurvival.UI.Inventory.InventoryView>().HasWaterBottle,
                Is.True);
            Assert.That(Object.FindFirstObjectByType<PlayerSpaceSuit>().IsEquipped, Is.False);
            Assert.That(Object.FindFirstObjectByType<PlayerOxygenConsumption>().ActiveSupply,
                Is.SameAs(flowController.Session.LandingPodOxygenSupply));
            Assert.That(Object.FindFirstObjectByType<PlanetSurvival.UI.HUD.LandingPodResourceView>(), Is.Not.Null);
            Assert.That(waterBottle.Container.CapacityMilliliters, Is.EqualTo(500));
            Assert.That(waterBottle.Container.CurrentMilliliters, Is.Zero);

            LandingPodWaterDispenser waterDispenser = Object.FindFirstObjectByType<LandingPodWaterDispenser>();
            WaterRefillView refillView = Object.FindFirstObjectByType<WaterRefillView>();
            GameObject initialPlayer = Object.FindFirstObjectByType<PlayerSurvival>().gameObject;
            waterDispenser.Interact(new InteractionContext(
                initialPlayer,
                initialPlayer.GetComponent<PlayerSurvival>(),
                initialPlayer.GetComponent<PlanetSurvival.Inventory.Application.PlayerInventory>()));
            Assert.That(refillView.IsOpen, Is.True);
            Assert.That(initialPlayer.GetComponent<PlanarPlayerMotor>().enabled, Is.False);
            Assert.That(initialPlayer.GetComponent<PlayerInteractor>().enabled, Is.False);
            Assert.That(refillView.FillBottle(), Is.EqualTo(500));
            initialPlayer.GetComponent<PlayerSurvival>().Stats.Thirst.SetCurrent(50f);
            Assert.That(waterBottle.TryDrink(), Is.EqualTo(WaterDrinkResult.Succeeded));
            Assert.That(waterBottle.Container.CurrentMilliliters, Is.EqualTo(400));
            Assert.That(initialPlayer.GetComponent<PlayerSurvival>().Stats.Thirst.Current, Is.EqualTo(70f));
            refillView.Close();
            Assert.That(initialPlayer.GetComponent<PlanarPlayerMotor>().enabled, Is.True);
            Assert.That(initialPlayer.GetComponent<PlayerInteractor>().enabled, Is.True);
            GameObject roomBoundary = GameObject.Find("Room Boundary");
            Assert.That(roomBoundary, Is.Not.Null);
            Assert.That(roomBoundary.GetComponentsInChildren<BoxCollider>(), Has.Length.EqualTo(6));

            GameObject player = Object.FindFirstObjectByType<PlayerSurvival>().gameObject;
            ScenePortal ladder = FindPortal(GameSceneNames.LandingPodCargo);
            Assert.That(ladder, Is.Not.Null);
            ladder.Interact(new InteractionContext(
                player,
                player.GetComponent<PlayerSurvival>(),
                player.GetComponent<PlanetSurvival.Inventory.Application.PlayerInventory>()));
            yield return WaitForScene(GameSceneNames.LandingPodCargo);
            yield return null;

            Assert.That(FindPortal(GameSceneNames.LandingPodHabitat), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerWaterBottle>().Container.CurrentMilliliters,
                Is.EqualTo(400), "The carried bottle must survive deck changes.");
            Assert.That(Object.FindFirstObjectByType<FixedInteriorBackdrop>(), Is.Not.Null);
            Assert.That(GameObject.Find("Dining Table"), Is.Null);
            StorageContainer cargoStorage = Object.FindFirstObjectByType<StorageContainer>();
            Assert.That(cargoStorage, Is.Not.Null);
            Assert.That(cargoStorage.gameObject.name, Is.EqualTo("Cargo Storage Racks"));
            Assert.That(cargoStorage.Inventory.TotalSlots, Is.EqualTo(30));
            Assert.That(cargoStorage.Inventory.Stacks, Has.Count.EqualTo(1));
            Assert.That(cargoStorage.Inventory.Stacks[0].Definition.ItemId, Is.EqualTo("energy_bar"));
            Assert.That(cargoStorage.Inventory.Stacks[0].Quantity,
                Is.EqualTo(GameSessionState.InitialEnergyBarCount));
            ScenePortal airlock = FindPortal(GameSceneNames.Gameplay);
            Assert.That(airlock, Is.Not.Null);
            player = Object.FindFirstObjectByType<PlayerSurvival>().gameObject;
            airlock.Interact(new InteractionContext(
                player,
                player.GetComponent<PlayerSurvival>(),
                player.GetComponent<PlanetSurvival.Inventory.Application.PlayerInventory>()));
            yield return WaitForScene(GameSceneNames.Gameplay);
            yield return null;

            Camera surfaceCamera = Object.FindFirstObjectByType<Camera>();
            HorizonBackdrop backdrop = surfaceCamera.GetComponentInChildren<HorizonBackdrop>();
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(FindPortal(GameSceneNames.LandingPodCargo), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerSpaceSuit>().IsEquipped, Is.True);
            Assert.That(Object.FindFirstObjectByType<PlayerOxygenConsumption>().ActiveSupply,
                Is.SameAs(flowController.Session.SpaceSuit.Oxygen));
            Assert.That(Object.FindFirstObjectByType<PlanetSurvival.UI.HUD.SuitResourceView>(), Is.Not.Null);

            flowController.Pause();
            Assert.That(flowController.State, Is.EqualTo(GameFlowState.Paused));
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));

            flowController.Resume();
            Assert.That(flowController.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f));

            flowController.ReturnToMainMenu();
            yield return WaitForScene(GameSceneNames.MainMenu);

            Assert.That(Object.FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<GameFlowController>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f));

            Object.Destroy(flowController.gameObject);
            yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            const float timeoutSeconds = 10f;
            float startedAt = UnityEngine.Time.realtimeSinceStartup;

            while (SceneManager.GetActiveScene().name != sceneName)
            {
                if (UnityEngine.Time.realtimeSinceStartup - startedAt > timeoutSeconds)
                {
                    Assert.Fail($"Timed out waiting for scene '{sceneName}'.");
                }

                yield return null;
            }

            yield return null;
        }

        private static ScenePortal FindPortal(string targetScene)
        {
            foreach (ScenePortal portal in Object.FindObjectsByType<ScenePortal>(FindObjectsSortMode.None))
            {
                if (portal.TargetScene == targetScene)
                {
                    return portal;
                }
            }

            return null;
        }
    }
}
