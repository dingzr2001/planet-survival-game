using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.UI.Farming;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.UI.Cooking;
using PlanetSurvival.UI.HUD;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.UI.Storage;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Runtime;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Interiors;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class LandingPodBootstrap : MonoBehaviour
    {
        [SerializeField] private LandingPodDeck _deck;
        [SerializeField] private PlanetEnvironmentSettings _environmentSettings;
        [SerializeField] private WorldVisualSettings _worldVisuals;
        [SerializeField] private InventorySkin _inventorySkin;
        [SerializeField, Tooltip("Cooking appliance offered on the habitat deck.")]
        private CookingStationDefinition _ovenStation;
        [SerializeField, Tooltip("Crop the habitat hydroponics rack grows.")]
        private CropDefinition _hydroponicsCrop;
        [SerializeField, Tooltip("Gathered ice the cargo-deck processor purifies into pod water.")]
        private ItemDefinition _iceItem;
        private const string RuntimeRootName = "Landing Pod Runtime";
        private const string HabitatBackgroundResource = "Interiors/LandingPodHabitat";
        private const string CargoBackgroundResource = "Interiors/LandingPodCargo";
        private const string DiningTableResource = "Interiors/HabitatDiningTable";
        private const float InteriorPlayerHeight = 2.6f;

        public LandingPodDeck Deck => _deck;

        public void Configure(LandingPodDeck deck, PlanetEnvironmentSettings environmentSettings,
            WorldVisualSettings worldVisuals, InventorySkin inventorySkin)
        {
            _deck = deck;
            _environmentSettings = environmentSettings;
            _worldVisuals = worldVisuals;
            _inventorySkin = inventorySkin;
        }

        public void ConfigureCooking(CookingStationDefinition ovenStation)
        {
            _ovenStation = ovenStation;
        }

        /// <summary>Wires the two fixtures of the ice-water-food loop to the assets they operate on.</summary>
        public void ConfigureLifeSupport(CropDefinition hydroponicsCrop, ItemDefinition iceItem)
        {
            _hydroponicsCrop = hydroponicsCrop;
            _iceItem = iceItem;
        }

        private void Start()
        {
            if (GameObject.Find(RuntimeRootName) != null)
            {
                Debug.LogWarning($"A '{RuntimeRootName}' object already exists. Duplicate interior creation was skipped.", this);
                return;
            }

            if (_environmentSettings == null)
            {
                Debug.LogError($"{nameof(LandingPodBootstrap)} on '{name}' requires environment settings.", this);
                enabled = false;
                return;
            }

            var root = new GameObject(RuntimeRootName);
            Sprite backgroundSprite = LoadRuntimeSprite(_deck == LandingPodDeck.Habitat
                ? HabitatBackgroundResource
                : CargoBackgroundResource);
            if (backgroundSprite == null)
            {
                enabled = false;
                Destroy(root);
                return;
            }

            Sprite diningTableSprite = _deck == LandingPodDeck.Habitat
                ? LoadRuntimeSprite(DiningTableResource)
                : null;
            GameSessionState session = ResolveSession();
            GameObject player = CreatePlayer(root.transform, session);
            Camera camera = CreateCamera(root.transform, backgroundSprite);
            GameClock clock = CreateClock(root.transform, player);
            clock.Bind(session.ConfigureTime(
                _environmentSettings.RealSecondsPerGameDay, _environmentSettings.RescueDay));
            GameObject hud = CreateHud(root.transform, player, clock, session);
            new LandingPodInteriorBuilder().Build(root.transform, _deck, diningTableSprite, camera,
                CreateFixtures(session, hud, clock));
            BindPlayerDeath(player);
        }

        private GameSessionState ResolveSession()
        {
            GameFlowController flowController = FindFirstObjectByType<GameFlowController>();
            if (flowController != null)
            {
                return flowController.Session;
            }

            Debug.LogWarning(
                $"{nameof(LandingPodBootstrap)} started without a {nameof(GameFlowController)}; " +
                "life-support state will last only for this scene.", this);
            return new GameSessionState();
        }

        private Sprite LoadRuntimeSprite(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogError($"{nameof(LandingPodBootstrap)} on '{name}' could not load '{resourcePath}'.", this);
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        private GameObject CreatePlayer(Transform parent, GameSessionState session)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent);
            player.transform.position = _deck == LandingPodDeck.Habitat
                ? new Vector3(1.5f, 0f, -2.8f)
                : new Vector3(-3.8f, 0f, -2.4f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.5f;
            controller.radius = .32f;
            controller.center = Vector3.up * .75f;

            PlayerSurvival survival = player.AddComponent<PlayerSurvival>();
            survival.Bind(session.SurvivalStats);
            player.AddComponent<PlayerInventory>().Bind(session.PlayerInventory);
            player.AddComponent<SurvivalDecay>();
            PlayerOxygen oxygen = player.AddComponent<PlayerOxygen>();
            player.AddComponent<PlayerHypoxia>();
            player.AddComponent<PlayerInteractor>();
            PlayerSpaceSuit spaceSuit = player.AddComponent<PlayerSpaceSuit>();
            spaceSuit.Bind(session.SpaceSuit, false);
            player.AddComponent<PlayerOxygenConsumption>().Bind(oxygen, spaceSuit, session.LandingPodOxygenSupply);
            player.AddComponent<PlayerWaterBottle>().Bind(session.SpaceSuit.Water, survival);
            CreatePlayerVisual(player.transform);
            player.AddComponent<PlanarPlayerMotor>();
            return player;
        }

        private void CreatePlayerVisual(Transform player)
        {
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(player, false);
            visual.AddComponent<SpriteRenderer>();
            WorldSpriteView spriteView = visual.AddComponent<WorldSpriteView>();
            if (_worldVisuals != null)
            {
                spriteView.ConfigureDirectional(
                    _worldVisuals.PlayerSprite,
                    InteriorPlayerHeight,
                    _worldVisuals.PlayerAnimationSheet,
                    _worldVisuals.PlayerFramesPerDirection,
                    _worldVisuals.PlayerFrameRects,
                    _worldVisuals.PlayerFramePivots);
            }
            else
            {
                spriteView.Configure(null, InteriorPlayerHeight);
            }

            Color shadowColor = _worldVisuals != null ? _worldVisuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(player, new Vector2(1.02f, .56f), shadowColor);
        }

        private static Camera CreateCamera(Transform parent, Sprite backgroundSprite)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .018f, .018f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.transparencySortMode = TransparencySortMode.Perspective;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 9f, -10f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.25f, 0f));
            FixedInteriorBackdrop.Create(camera, backgroundSprite);
            return camera;
        }

        private static GameClock CreateClock(Transform parent, GameObject player)
        {
            var clockObject = new GameObject("Game Clock");
            clockObject.transform.SetParent(parent);
            GameClock clock = clockObject.AddComponent<GameClock>();
            clock.Bind(player.GetComponent<SurvivalDecay>());
            clock.Bind(player.GetComponent<PlayerHypoxia>());
            clock.Bind(player.GetComponent<PlayerOxygenConsumption>());
            return clock;
        }

        private CookingStationBinding CreateCookingBinding(GameSessionState session, CookingView view)
        {
            if (_deck != LandingPodDeck.Habitat || _ovenStation == null)
            {
                return default;
            }

            return new CookingStationBinding(
                _ovenStation, session.GetCookingProcess(_ovenStation.StationId), view);
        }

        private LandingPodFixtures CreateFixtures(GameSessionState session, GameObject hud, GameClock clock)
        {
            return new LandingPodFixtures(
                session.LandingPodWaterSupply,
                session.WaterBottle,
                hud.GetComponent<WaterRefillView>(),
                session.RefrigeratorStorage,
                session.CargoStorage,
                hud.GetComponent<StorageView>(),
                CreateCookingBinding(session, hud.GetComponent<CookingView>()),
                CreateProcessorBinding(session, hud.GetComponent<WaterProcessorView>(), clock),
                CreateHydroponicsBinding(session, hud.GetComponent<HydroponicsView>(), clock));
        }

        private WaterProcessorBinding CreateProcessorBinding(GameSessionState session, WaterProcessorView view,
            GameClock clock)
        {
            if (_deck != LandingPodDeck.Cargo)
            {
                return default;
            }

            if (_iceItem == null)
            {
                Debug.LogError(
                    $"{nameof(LandingPodBootstrap)} on '{name}' has no ice item; the water processor is unavailable.",
                    this);
                return default;
            }

            return new WaterProcessorBinding(
                session.WaterProcessor, _iceItem, session.LandingPodWaterSupply, view, clock);
        }

        private HydroponicsBinding CreateHydroponicsBinding(GameSessionState session, HydroponicsView view,
            GameClock clock)
        {
            if (_deck != LandingPodDeck.Habitat)
            {
                return default;
            }

            if (_hydroponicsCrop == null)
            {
                Debug.LogError(
                    $"{nameof(LandingPodBootstrap)} on '{name}' has no crop; the hydroponics rack is unavailable.",
                    this);
                return default;
            }

            return new HydroponicsBinding(
                session.Hydroponics, _hydroponicsCrop, session.LandingPodWaterSupply, view, clock);
        }

        private GameObject CreateHud(Transform parent, GameObject player, GameClock clock,
            GameSessionState session)
        {
            var hudObject = new GameObject("Survival HUD");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<SurvivalHudView>().Bind(player.GetComponent<PlayerSurvival>(), clock);
            hudObject.AddComponent<InteractionPromptView>().Bind(player.GetComponent<PlayerInteractor>());
            hudObject.AddComponent<InventoryView>().Bind(
                player.GetComponent<PlayerInventory>(),
                _inventorySkin,
                player.GetComponent<PlayerWaterBottle>());
            hudObject.AddComponent<GameOverView>();
            hudObject.AddComponent<WaterRefillView>();
            hudObject.AddComponent<StorageView>().Bind(_inventorySkin);
            hudObject.AddComponent<CookingView>();
            hudObject.AddComponent<WaterProcessorView>();
            hudObject.AddComponent<HydroponicsView>();
            hudObject.AddComponent<LandingPodResourceView>().Bind(
                session.LandingPodOxygenSupply,
                session.LandingPodWaterSupply);

            LandingPodLocationView locationView = hudObject.AddComponent<LandingPodLocationView>();
            if (_deck == LandingPodDeck.Habitat)
            {
                locationView.Configure("LANDING POD · DECK 2", "Habitat / Life Support");
            }
            else
            {
                locationView.Configure("LANDING POD · DECK 1", "Cargo / Surface Operations");
            }

            return hudObject;
        }

        private static void BindPlayerDeath(GameObject player)
        {
            GameFlowController flowController = FindFirstObjectByType<GameFlowController>();
            if (flowController == null)
            {
                Debug.LogError($"Landing pod requires a persistent {nameof(GameFlowController)}.", player);
                return;
            }

            player.AddComponent<PlayerDeathFlowHandler>()
                .Bind(player.GetComponent<PlayerSurvival>(), flowController);
        }
    }
}
