using System.Collections;
using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Farming.Runtime;
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

            HydroponicsStation hydroponics = Object.FindFirstObjectByType<HydroponicsStation>();
            Assert.That(hydroponics, Is.Not.Null, "The habitat deck grows the food half of the loop.");
            Assert.That(hydroponics.Rack, Is.SameAs(flowController.Session.Hydroponics));
            Assert.That(Object.FindFirstObjectByType<PlanetSurvival.UI.Farming.HydroponicsView>(), Is.Not.Null);
            AssertPropUsesItsArtwork(hydroponics.gameObject);

            // Half a game day is short enough that no vital empties, and long enough to prove the clock
            // is not restarted by the deck change below.
            GameClock habitatClock = Object.FindFirstObjectByType<GameClock>();
            habitatClock.Advance(300f);
            double elapsedBeforeDeckChange = habitatClock.ElapsedDays;
            Assert.That(elapsedBeforeDeckChange, Is.GreaterThan(.4d));

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

            GameClock cargoClock = Object.FindFirstObjectByType<GameClock>();
            Assert.That(cargoClock.ElapsedDays, Is.EqualTo(elapsedBeforeDeckChange).Within(.05d),
                "A deck change must not rewind the expedition clock or the rescue countdown.");
            WaterProcessorStation processor = Object.FindFirstObjectByType<WaterProcessorStation>();
            Assert.That(processor, Is.Not.Null, "The cargo deck makes the water half of the loop.");
            Assert.That(processor.Processor, Is.SameAs(flowController.Session.WaterProcessor));
            Assert.That(Object.FindFirstObjectByType<WaterProcessorView>(), Is.Not.Null);
            AssertPropUsesItsArtwork(processor.gameObject);
            Assert.That(Object.FindFirstObjectByType<FixedInteriorBackdrop>(), Is.Not.Null);
            Assert.That(GameObject.Find("Dining Table"), Is.Null);
            StorageContainer cargoStorage = Object.FindFirstObjectByType<StorageContainer>();
            Assert.That(cargoStorage, Is.Not.Null);
            Assert.That(cargoStorage.gameObject.name, Is.EqualTo("Cargo Storage Racks"));
            Assert.That(cargoStorage.Inventory.TotalSlots, Is.EqualTo(30));
            Assert.That(cargoStorage.Inventory.Stacks, Has.Count.EqualTo(3));
            Assert.That(cargoStorage.Inventory.GetQuantity("energy_bar"),
                Is.EqualTo(GameSessionState.InitialEnergyBarCount));
            Assert.That(cargoStorage.Inventory.GetQuantity("potato"),
                Is.EqualTo(GameSessionState.InitialPotatoCount));
            Assert.That(cargoStorage.Inventory.GetQuantity("aluminum_alloy"),
                Is.EqualTo(GameSessionState.InitialAluminumAlloyCount));
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
            Assert.That(surfaceCamera.orthographic, Is.False);
            Assert.That(surfaceCamera.fieldOfView, Is.EqualTo(55f).Within(.01f),
                "The surface uses a longer lens so 2D cutouts keep their proportions while the sky remains visible.");
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

        /// <summary>
        /// A fixture whose texture is not imported as a sprite loads no artwork and quietly draws its
        /// placeholder block instead, which looks like working art in a build until someone opens the
        /// deck. Assert the real sprite so a bad import fails here rather than in playtesting.
        /// </summary>
        private static void AssertPropUsesItsArtwork(GameObject prop)
        {
            SpriteRenderer renderer = prop.GetComponentInChildren<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null, $"'{prop.name}' has no sprite renderer.");
            Assert.That(renderer.sprite, Is.Not.Null, $"'{prop.name}' is rendering nothing.");
            Assert.That(renderer.sprite.name, Is.Not.EqualTo("PlaceholderBlock"),
                $"'{prop.name}' fell back to its placeholder block; its artwork is missing or is not " +
                "imported as a sprite. Run 'Planet Survival/Setup World Art'.");
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
