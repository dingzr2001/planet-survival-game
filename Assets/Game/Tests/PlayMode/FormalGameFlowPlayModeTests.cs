using System.Collections;
using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.World.Presentation;
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
            yield return WaitForScene(GameSceneNames.Gameplay);
            yield return null;

            Assert.That(Object.FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameClock>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Camera gameCamera = Object.FindFirstObjectByType<Camera>();
            Assert.That(gameCamera.orthographic, Is.False);
            HorizonBackdrop backdrop = gameCamera.GetComponentInChildren<HorizonBackdrop>();
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlanarPlayerMotor>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<WorldSpriteView>(FindObjectsSortMode.None).Length,
                Is.GreaterThan(1));

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
    }
}
