using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Core.Flow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private static GameFlowController _instance;
        [SerializeField, Tooltip("Non-craftable emergency food placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _energyBarDefinition;
        private GameFlow _flow;
        private readonly GameSessionState _session = new();

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
        public GameSessionState Session => _session;

        public void ConfigureStartingSupplies(ItemDefinition energyBarDefinition)
        {
            _energyBarDefinition = energyBarDefinition;
        }

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

        public void StartGame()
        {
            ResetSession();
            _flow.StartGame();
        }
        public void Pause() => _flow.Pause();
        public void Resume() => _flow.Resume();
        public void RestartGame()
        {
            if (State == GameFlowState.Playing || State == GameFlowState.Paused || State == GameFlowState.GameOver)
            {
                ResetSession();
            }

            _flow.RestartGame();
        }
        public void ReturnToMainMenu() => _flow.ReturnToMainMenu();
        public void EndGame() => _flow.EndGame();

        private void ResetSession()
        {
            if (_energyBarDefinition == null)
            {
                _session.Reset();
                Debug.LogError($"{nameof(GameFlowController)} requires an energy bar definition for starting cargo.", this);
                return;
            }

            InventoryOperationResult result = _session.Reset(
                _energyBarDefinition,
                GameSessionState.InitialEnergyBarCount);
            if (!result.Succeeded)
            {
                Debug.LogError($"Could not provision starting energy bars: {result.Message}", this);
            }
        }

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
