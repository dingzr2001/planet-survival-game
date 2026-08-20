using System;

namespace PlanetSurvival.Core.Flow
{
    public sealed class GameFlow
    {
        private readonly IGameSceneLoader _sceneLoader;
        private readonly IGamePauseService _pauseService;

        public GameFlow(IGameSceneLoader sceneLoader, IGamePauseService pauseService)
        {
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
        }

        public GameFlowState State { get; private set; } = GameFlowState.Booting;
        public event Action<GameFlowState> StateChanged;

        public void EnterMainMenu()
        {
            LoadScene(GameSceneNames.MainMenu, GameFlowState.MainMenu);
        }

        public void StartGame()
        {
            LoadScene(GameSceneNames.Gameplay, GameFlowState.Playing);
        }

        public bool Pause()
        {
            if (State != GameFlowState.Playing)
            {
                return false;
            }

            _pauseService.SetPaused(true);
            SetState(GameFlowState.Paused);
            return true;
        }

        public bool Resume()
        {
            if (State != GameFlowState.Paused)
            {
                return false;
            }

            _pauseService.SetPaused(false);
            SetState(GameFlowState.Playing);
            return true;
        }

        public bool RestartGame()
        {
            if (State != GameFlowState.Playing && State != GameFlowState.Paused && State != GameFlowState.GameOver)
            {
                return false;
            }

            LoadScene(GameSceneNames.Gameplay, GameFlowState.Playing);
            return true;
        }

        public bool EndGame()
        {
            if (State != GameFlowState.Playing && State != GameFlowState.Paused)
            {
                return false;
            }

            _pauseService.SetPaused(true);
            SetState(GameFlowState.GameOver);
            return true;
        }

        public bool ReturnToMainMenu()
        {
            if (State != GameFlowState.Playing && State != GameFlowState.Paused && State != GameFlowState.GameOver)
            {
                return false;
            }

            LoadScene(GameSceneNames.MainMenu, GameFlowState.MainMenu);
            return true;
        }

        private void LoadScene(string sceneName, GameFlowState completedState)
        {
            _pauseService.SetPaused(false);
            SetState(GameFlowState.Loading);
            _sceneLoader.Load(sceneName, () => SetState(completedState));
        }

        private void SetState(GameFlowState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(State);
        }
    }
}
