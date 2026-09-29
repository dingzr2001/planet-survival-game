using System.Collections.Generic;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.UI;
using UnityEngine;

namespace PlanetSurvival.Core.Flow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private static GameFlowController _instance;
        [SerializeField, Tooltip("Non-craftable emergency food placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _energyBarDefinition;
        [SerializeField, Tooltip("Raw vegetable ingredient placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _potatoDefinition;
        [SerializeField, Tooltip("Seed potato cuttings placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _potatoSeedDefinition;
        [SerializeField, Tooltip("Structural building material placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _aluminumAlloyDefinition;
        [SerializeField, Tooltip("Emergency oxidizer sufficient to fabricate one oxygen candle.")]
        private ItemDefinition _chlorateSaltDefinition;
        [SerializeField, Tooltip("Mining tool placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _pickaxeDefinition;
        [SerializeField, Tooltip("Digging tool placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _shovelDefinition;
        [SerializeField, Tooltip("Portable petroleum fuel placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _petroleumDefinition;
        [SerializeField] private ItemDefinition _soilDefinition;
        [SerializeField] private ItemDefinition _plasticSheetDefinition;
        [SerializeField] private ItemDefinition _carbonDioxideCanisterDefinition;
        [SerializeField] private ItemDefinition _carbonDioxideFilterCartridgeDefinition;
        [SerializeField, Tooltip("Residual hypergolic propellant placed in cargo storage at the start of an expedition.")]
        private ItemDefinition _monomethylhydrazineDefinition;
        [SerializeField, Tooltip("Debug-only prototype component used to exercise transfer-post construction before its production chain exists.")]
        private ItemDefinition _entanglementRelayCoreDefinition;
        [SerializeField] private ItemDefinition _generatorOxygenDefinition;
        [SerializeField] private ItemDefinition _methaneDefinition;
        [SerializeField] private ItemDefinition _hydrogenDefinition;
        [Header("Debug")]
        [SerializeField, Tooltip(
            "Editor and Development Builds only. Starts every configured supply at 999 in the player's backpack.")]
        private bool _giveTestStartingItems = true;
        private GameFlow _flow;
        private GameSessionState _session;
        private ModalPanelRegistry _modalPanels;

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
        public GameSessionState Session => _session ??= new GameSessionState(IsTestMode);

        private bool IsTestMode => Debug.isDebugBuild && _giveTestStartingItems;

        public void ConfigureStartingSupplies(ItemDefinition energyBarDefinition, ItemDefinition potatoDefinition,
            ItemDefinition aluminumAlloyDefinition, ItemDefinition chlorateSaltDefinition,
            ItemDefinition pickaxeDefinition = null, ItemDefinition petroleumDefinition = null,
            ItemDefinition shovelDefinition = null, ItemDefinition soilDefinition = null,
            ItemDefinition plasticSheetDefinition = null, ItemDefinition carbonDioxideCanisterDefinition = null,
            ItemDefinition entanglementRelayCoreDefinition = null,
            ItemDefinition carbonDioxideFilterCartridgeDefinition = null,
            ItemDefinition potatoSeedDefinition = null,
            ItemDefinition monomethylhydrazineDefinition = null)
        {
            _energyBarDefinition = energyBarDefinition;
            _potatoDefinition = potatoDefinition;
            _aluminumAlloyDefinition = aluminumAlloyDefinition;
            _chlorateSaltDefinition = chlorateSaltDefinition;
            _pickaxeDefinition = pickaxeDefinition;
            _petroleumDefinition = petroleumDefinition;
            _shovelDefinition = shovelDefinition;
            _soilDefinition = soilDefinition;
            _plasticSheetDefinition = plasticSheetDefinition;
            _carbonDioxideCanisterDefinition = carbonDioxideCanisterDefinition;
            _entanglementRelayCoreDefinition = entanglementRelayCoreDefinition;
            _carbonDioxideFilterCartridgeDefinition = carbonDioxideFilterCartridgeDefinition;
            _potatoSeedDefinition = potatoSeedDefinition;
            _monomethylhydrazineDefinition = monomethylhydrazineDefinition;
        }

        public void ConfigureGeneratorSupplies(ItemDefinition oxygen, ItemDefinition methane, ItemDefinition hydrogen)
        {
            _generatorOxygenDefinition = oxygen;
            _methaneDefinition = methane;
            _hydrogenDefinition = hydrogen;
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
            if (_energyBarDefinition == null || _potatoDefinition == null || _aluminumAlloyDefinition == null
                || _chlorateSaltDefinition == null)
            {
                Session.Reset();
                Debug.LogError(
                    $"{nameof(GameFlowController)} requires energy bar, potato, aluminum alloy and chlorate salt " +
                    "definitions for starting cargo.", this);
                return;
            }

            int Quantity(int normalQuantity) => IsTestMode ? GameSessionState.TestItemQuantity : normalQuantity;

            var startingSupplies = new List<InventoryItemAmount>
            {
                new InventoryItemAmount(_energyBarDefinition, Quantity(GameSessionState.InitialEnergyBarCount)),
                new InventoryItemAmount(_potatoDefinition, Quantity(GameSessionState.InitialPotatoCount)),
                new InventoryItemAmount(_aluminumAlloyDefinition, Quantity(GameSessionState.InitialAluminumAlloyCount)),
                new InventoryItemAmount(_chlorateSaltDefinition, Quantity(GameSessionState.InitialChlorateSaltCount))
            };
            if (_potatoSeedDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(
                    _potatoSeedDefinition, Quantity(GameSessionState.InitialPotatoSeedCount)));
            if (_pickaxeDefinition != null)
            {
                startingSupplies.Add(new InventoryItemAmount(
                    _pickaxeDefinition, Quantity(GameSessionState.InitialPickaxeCount)));
            }

            if (_petroleumDefinition != null)
            {
                startingSupplies.Add(new InventoryItemAmount(
                    _petroleumDefinition, Quantity(GameSessionState.InitialPetroleumCanisterCount)));
            }

            if (_shovelDefinition != null)
            {
                startingSupplies.Add(new InventoryItemAmount(
                    _shovelDefinition, Quantity(GameSessionState.InitialShovelCount)));
            }

            if (_soilDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(
                    _soilDefinition, Quantity(GameSessionState.InitialSoilCount)));
            if (_plasticSheetDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(
                    _plasticSheetDefinition, Quantity(GameSessionState.InitialPlasticSheetCount)));
            if (_carbonDioxideCanisterDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_carbonDioxideCanisterDefinition,
                    Quantity(GameSessionState.InitialCarbonDioxideCanisterCount)));
            if (_carbonDioxideFilterCartridgeDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_carbonDioxideFilterCartridgeDefinition,
                    Quantity(GameSessionState.InitialCarbonDioxideFilterCartridgeCount)));
            if (_monomethylhydrazineDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_monomethylhydrazineDefinition,
                    Quantity(GameSessionState.InitialMonomethylhydrazineCount)));
            if (IsTestMode && _entanglementRelayCoreDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(
                    _entanglementRelayCoreDefinition, GameSessionState.TestItemQuantity));

            if (_generatorOxygenDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_generatorOxygenDefinition, Quantity(8)));
            if (_methaneDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_methaneDefinition, Quantity(6)));
            if (IsTestMode && _hydrogenDefinition != null)
                startingSupplies.Add(new InventoryItemAmount(_hydrogenDefinition, GameSessionState.TestItemQuantity));

            InventoryOperationResult result = IsTestMode
                ? Session.ResetPlayerInventory(startingSupplies)
                : Session.Reset(startingSupplies);
            if (!result.Succeeded)
            {
                Debug.LogError($"Could not provision starting supplies: {result.Message}", this);
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
            _session ??= new GameSessionState(IsTestMode);
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

        /// <summary>
        /// Registers the HUD that reports whether a full-screen panel is open. Escape closes such a panel, and
        /// pausing on the same press would put the pause menu up behind it, so the pause toggle stands down
        /// while one is open. Scenes without panels never call this and pause on every press.
        /// </summary>
        public void BindModalPanels(ModalPanelRegistry modalPanels)
        {
            _modalPanels = modalPanels;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (_modalPanels != null && _modalPanels.BlocksGlobalEscape)
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
