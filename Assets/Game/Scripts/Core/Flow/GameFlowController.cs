using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Core.Time;
using UnityEngine;

namespace PlanetSurvival.Core.Flow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private static GameFlowController _instance;
        private GameFlow _flow;

        public event System.Action<GameFlowState> StateChanged
        {
            add
            {
                Initialize();
                _flow.StateChanged += value;
            }
            remove
            {
                if (_flow != null)
                {
                    _flow.StateChanged -= value;
                }
            }
        }

        public GameFlowState State => _flow?.State ?? GameFlowState.Booting;

        public void Initialize()
        {
            if (_flow != null)
            {
                return;
            }

            _flow = new GameFlow(new UnitySceneLoader(), new UnityTimeScale());
        }

        public void EnterInitialScene()
        {
            Initialize();
            _flow.EnterMainMenu();
        }

        public void StartGame() => _flow.StartGame();
        public void Pause() => _flow.Pause();
        public void Resume() => _flow.Resume();
        public void RestartGame() => _flow.RestartGame();
        public void ReturnToMainMenu() => _flow.ReturnToMainMenu();
        public void EndGame() => _flow.EndGame();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Initialize();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                new UnityTimeScale().SetPaused(false);
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (_flow.State == GameFlowState.Playing)
            {
                _flow.Pause();
            }
            else if (_flow.State == GameFlowState.Paused)
            {
                _flow.Resume();
            }
        }
    }
}
