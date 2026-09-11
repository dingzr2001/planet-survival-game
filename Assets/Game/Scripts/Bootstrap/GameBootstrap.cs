using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.UI.Building;
using PlanetSurvival.UI.Cooking;
using PlanetSurvival.UI.HUD;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.Water.Runtime;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TerrainGenerationSettings _terrainSettings;
        [SerializeField] private PlanetEnvironmentSettings _environmentSettings;
        [SerializeField] private ResourceSpawnSettings _resourceSpawnSettings;
        [SerializeField] private WorldVisualSettings _worldVisuals;
        [SerializeField, Tooltip("Slot artwork for the quick bar and inventory panel. Optional; the HUD falls back to the built-in GUI skin.")]
        private InventorySkin _inventorySkin;
        [SerializeField, Tooltip("Structures the build panel offers. Without it the surface has no building.")]
        private BuildingCatalog _buildingCatalog;

        private const string RuntimeRootName = "Gameplay Runtime";
        private const float SurfaceCameraOrthographicSize = 9.5f;

        public void Configure(TerrainGenerationSettings terrainSettings)
        {
            _terrainSettings = terrainSettings;
        }

        public void Configure(TerrainGenerationSettings terrainSettings, PlanetEnvironmentSettings environmentSettings)
        {
            _terrainSettings = terrainSettings;
            _environmentSettings = environmentSettings;
        }

        public void Configure(TerrainGenerationSettings terrainSettings, PlanetEnvironmentSettings environmentSettings,
            ResourceSpawnSettings resourceSpawnSettings)
        {
            _terrainSettings = terrainSettings;
            _environmentSettings = environmentSettings;
            _resourceSpawnSettings = resourceSpawnSettings;
        }

        public void ConfigureUi(InventorySkin inventorySkin)
        {
            _inventorySkin = inventorySkin;
        }

        public void ConfigureVisuals(WorldVisualSettings worldVisuals)
        {
            _worldVisuals = worldVisuals;
        }

        public void ConfigureBuilding(BuildingCatalog buildingCatalog)
        {
            _buildingCatalog = buildingCatalog;
        }

        private void Start()
        {
            if (_terrainSettings == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} on '{name}' requires terrain generation settings.", this);
                enabled = false;
                return;
            }

            if (_environmentSettings == null)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)} on '{name}' has no environment settings; runtime defaults will be used.", this);
                _environmentSettings = ScriptableObject.CreateInstance<PlanetEnvironmentSettings>();
                _environmentSettings.ConfigureDefaults();
            }

            if (GameObject.Find(RuntimeRootName) != null)
            {
                Debug.LogWarning($"A '{RuntimeRootName}' object already exists. Duplicate gameplay creation was skipped.", this);
                return;
            }

            var root = new GameObject(RuntimeRootName);
            GameSessionState session = ResolveSession();
            ContinuousTerrainView terrainView = CreateTerrain(root.transform);
            GameObject player = CreatePlayer(root.transform, session);
            terrainView.SetTarget(player.transform);
            LandingPodExterior.Create(root.transform, player.transform.position + new Vector3(4f, 0f, 0f),
                _worldVisuals);
            CreateResourceStreaming(root.transform, player.transform, session.Buildings.Grid);
            CreateCamera(root.transform, player.transform);
            Light sun = CreateLighting(root.transform);
            GameClock clock = CreateClock(root.transform, player);
            clock.Bind(session.ConfigureTime(
                _environmentSettings.RealSecondsPerGameDay, _environmentSettings.RescueDay));
            root.AddComponent<DayNightEnvironment>().Bind(clock, sun, _environmentSettings);
            GameObject hud = CreateHud(root.transform, player, clock, _inventorySkin);
            CreateBuildingSystem(root.transform, player, hud, session);
            BindPlayerDeath(player);
        }

        private ContinuousTerrainView CreateTerrain(Transform parent)
        {
            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(parent);
            ContinuousTerrainView terrainView = terrain.AddComponent<ContinuousTerrainView>();
            terrainView.Build(_terrainSettings, _worldVisuals);
            return terrainView;
        }

        private void CreateResourceStreaming(Transform parent, Transform target, BuildGrid buildGrid)
        {
            if (_resourceSpawnSettings == null)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)} on '{name}' has no resource spawn settings; the world will contain no resources.", this);
                return;
            }

            var streaming = new GameObject("Resource Streaming");
            streaming.transform.SetParent(parent);
            ResourceChunkStreamer streamer = streaming.AddComponent<ResourceChunkStreamer>();
            streamer.Configure(_resourceSpawnSettings, _worldVisuals,
                _terrainSettings.Seed + _resourceSpawnSettings.SeedOffset, target.position, buildGrid);
            streamer.SetTarget(target);
        }

        private static GameSessionState ResolveSession()
        {
            GameFlowController flowController = FindFirstObjectByType<GameFlowController>();
            return flowController != null ? flowController.Session : new GameSessionState();
        }

        private GameObject CreatePlayer(Transform parent, GameSessionState session)
        {
            var player = new GameObject();
            player.name = "Player";
            player.transform.SetParent(parent);
            player.transform.position = _terrainSettings.StartingAreaCenter;

            var controller = player.AddComponent<CharacterController>();
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
            spaceSuit.Bind(session.SpaceSuit, true);
            player.AddComponent<PlayerOxygenConsumption>().Bind(oxygen, spaceSuit);
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
                    _worldVisuals.PlayerHeight,
                    _worldVisuals.PlayerAnimationSheet,
                    _worldVisuals.PlayerFramesPerDirection,
                    _worldVisuals.PlayerFrameRects,
                    _worldVisuals.PlayerFramePivots);
            }
            else
            {
                spriteView.Configure(null, 1.8f);
            }

            Color shadowColor = _worldVisuals != null ? _worldVisuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(player, new Vector2(.72f, .4f), shadowColor);
        }

        private void CreateCamera(Transform parent, Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            // The surface uses a stable oblique view so cutout art, building footprints and gathering
            // ranges keep a predictable screen scale without needing distant perspective artwork.
            camera.orthographicSize = SurfaceCameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .025f, .018f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1100f;
            camera.transparencySortMode = TransparencySortMode.Orthographic;
            cameraObject.AddComponent<AudioListener>();

            FollowCamera followCamera = camera.gameObject.AddComponent<FollowCamera>();
            followCamera.SetTarget(target);
        }

        private static Light CreateLighting(Transform parent)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.82f, 0.68f);
            return light;
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

        /// <summary>
        /// Placement lives next to the world rather than on the HUD: the panel only picks a structure, the
        /// controller owns the grid preview and the sites the session carries between scenes.
        /// </summary>
        private void CreateBuildingSystem(Transform parent, GameObject player, GameObject hud, GameSessionState session)
        {
            if (_buildingCatalog == null)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)} on '{name}' has no building catalog; the build panel is unavailable.", this);
                return;
            }

            var systemObject = new GameObject("Building System");
            systemObject.transform.SetParent(parent);
            BuildingPlacementController controller = systemObject.AddComponent<BuildingPlacementController>();
            PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
            controller.Bind(session.Buildings, playerInventory, _buildingCatalog, _worldVisuals, session,
                hud.GetComponent<CookingView>());

            hud.AddComponent<BuildMenuView>().Bind(
                controller,
                playerInventory,
                player.GetComponent<PlanarPlayerMotor>(),
                player.GetComponent<PlayerInteractor>());
        }

        private static GameObject CreateHud(Transform parent, GameObject player, GameClock clock, InventorySkin inventorySkin)
        {
            var hudObject = new GameObject("Survival HUD");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<SurvivalHudView>().Bind(player.GetComponent<PlayerSurvival>(), clock);
            hudObject.AddComponent<InteractionPromptView>().Bind(player.GetComponent<PlayerInteractor>());
            hudObject.AddComponent<InventoryView>().Bind(
                player.GetComponent<PlayerInventory>(),
                inventorySkin,
                player.GetComponent<PlayerWaterBottle>());
            hudObject.AddComponent<SuitResourceView>().Bind(
                player.GetComponent<PlayerSpaceSuit>().Resources);
            // Built appliances open their menu through this panel, exactly like the ones inside the pod.
            hudObject.AddComponent<CookingView>();
            hudObject.AddComponent<GameOverView>();
            return hudObject;
        }

        private static void BindPlayerDeath(GameObject player)
        {
            GameFlowController flowController = FindFirstObjectByType<GameFlowController>();
            if (flowController == null)
            {
                Debug.LogError($"Gameplay requires a persistent {nameof(GameFlowController)}.", player);
                return;
            }

            player.AddComponent<PlayerDeathFlowHandler>()
                .Bind(player.GetComponent<PlayerSurvival>(), flowController);
        }
    }
}
