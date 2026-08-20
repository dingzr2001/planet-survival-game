using System;
using NUnit.Framework;
using PlanetSurvival.Core.Flow;

namespace PlanetSurvival.Tests
{
    public sealed class GameFlowTests
    {
        [Test]
        public void StartGame_TransitionsThroughLoadingAndEntersPlayingAfterLoad()
        {
            var loader = new SceneLoaderStub();
            var pause = new PauseServiceStub();
            var flow = new GameFlow(loader, pause);

            flow.StartGame();

            Assert.That(flow.State, Is.EqualTo(GameFlowState.Loading));
            Assert.That(loader.RequestedScene, Is.EqualTo(GameSceneNames.Gameplay));

            loader.Complete();

            Assert.That(flow.State, Is.EqualTo(GameFlowState.Playing));
        }

        [Test]
        public void PauseAndResume_OnlyWorkDuringGameplay()
        {
            var loader = new SceneLoaderStub();
            var pause = new PauseServiceStub();
            var flow = new GameFlow(loader, pause);

            Assert.That(flow.Pause(), Is.False);

            flow.StartGame();
            loader.Complete();

            Assert.That(flow.Pause(), Is.True);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Paused));
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(flow.Resume(), Is.True);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(pause.IsPaused, Is.False);
        }

        [Test]
        public void ReturnToMainMenu_UnpausesBeforeLoadingMenu()
        {
            var loader = new SceneLoaderStub();
            var pause = new PauseServiceStub();
            var flow = new GameFlow(loader, pause);
            flow.StartGame();
            loader.Complete();
            flow.Pause();

            bool accepted = flow.ReturnToMainMenu();

            Assert.That(accepted, Is.True);
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(loader.RequestedScene, Is.EqualTo(GameSceneNames.MainMenu));
        }

        [Test]
        public void EndGame_PausesAndEntersGameOver()
        {
            var loader = new SceneLoaderStub();
            var pause = new PauseServiceStub();
            var flow = new GameFlow(loader, pause);
            flow.StartGame();
            loader.Complete();

            bool accepted = flow.EndGame();

            Assert.That(accepted, Is.True);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.GameOver));
            Assert.That(pause.IsPaused, Is.True);
        }

        private sealed class SceneLoaderStub : IGameSceneLoader
        {
            private Action _completed;

            public string RequestedScene { get; private set; }

            public void Load(string sceneName, Action completed)
            {
                RequestedScene = sceneName;
                _completed = completed;
            }

            public void Complete()
            {
                Action completed = _completed;
                _completed = null;
                completed?.Invoke();
            }
        }

        private sealed class PauseServiceStub : IGamePauseService
        {
            public bool IsPaused { get; private set; }

            public void SetPaused(bool isPaused)
            {
                IsPaused = isPaused;
            }
        }
    }
}
