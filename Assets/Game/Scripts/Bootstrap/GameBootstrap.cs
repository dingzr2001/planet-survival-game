using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Animation;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.UI.Cooking;
using PlanetSurvival.UI.Crafting;
using PlanetSurvival.UI.HUD;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.UI.Mining;
using PlanetSurvival.UI.Farming;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.UI.Transport;
using PlanetSurvival.Water.Runtime;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Ground;
using PlanetSurvival.World.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float SurfaceRobotControllerHeight = 1.2f;
        private const float SurfaceRobotControllerRadius = .55f;
        [SerializeField] private TerrainGenerationSettings _terrainSettings;
        [SerializeField] private PlanetEnvironmentSettings _environmentSettings;
        [SerializeField] private ResourceSpawnSettings _resourceSpawnSettings;
        [SerializeField, Tooltip("Terrain that covers the base regolith in patches, such as diggable rock. Without it the surface is bare regolith everywhere.")]
        private TerrainPatchSettings _terrainPatchSettings;
        [SerializeField] private WorldVisualSettings _worldVisuals;
        [SerializeField, Tooltip("Slot artwork for the quick bar and inventory panel. Optional; the HUD falls back to the built-in GUI skin.")]
        private InventorySkin _inventorySkin;
        [SerializeField, Tooltip("Structures the build panel offers. Without it the surface has no building.")]
        private BuildingCatalog _buildingCatalog;
        [SerializeField, Tooltip("Recipes available from the left-side handheld crafting drawer.")]
        private CraftingCatalog _craftingCatalog;
        [Header("Debug")]
        [SerializeField, Tooltip("Editor and Development Builds only. Set before Play Mode to make terrain deposits and resource nodes easier to inspect.")]
        private bool _abundantSurfaceResourcesInDebugBuild = true;
        [SerializeField, Range(1f, 10f), Tooltip("Multiplies both terrain-patch coverage and resource-node density while debug abundance is enabled.")]
        private float _debugSurfaceResourceMultiplier = 6f;

        private const string RuntimeRootName = "Gameplay Runtime";
        private const float SurfaceCameraOrthographicSize = 9.5f;

        private float SurfaceResourceDensityMultiplier =>
            Debug.isDebugBuild && _abundantSurfaceResourcesInDebugBuild
                ? Mathf.Max(1f, _debugSurfaceResourceMultiplier)
                : 1f;

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

        public void ConfigureTerrainPatches(TerrainPatchSettings terrainPatchSettings)
        {
            _terrainPatchSettings = terrainPatchSettings;
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

        public void ConfigureCrafting(CraftingCatalog craftingCatalog)
        {
            _craftingCatalog = craftingCatalog;
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
            if (SurfaceResourceDensityMultiplier > 1f)
            {
                Debug.Log(
                    $"Debug surface-resource abundance is active at {SurfaceResourceDensityMultiplier:0.#}x. "
                    + "Release builds still use authored densities.", this);
            }

            GameSessionState session = ResolveSession();
            ContinuousTerrainView terrainView = CreateTerrain(root.transform);
            GameObject player = CreatePlayer(root.transform, session);
            terrainView.SetTarget(player.transform);
            CreateTerrainPatches(root.transform, player.transform, session);
            LandingPodExterior.Create(root.transform, player.transform.position + new Vector3(4f, 0f, 0f),
                _worldVisuals);
            CreateResourceStreaming(root.transform, player.transform, session.Buildings.Grid);
            Camera camera = CreateCamera(root.transform, player.transform);
            Light sun = CreateLighting(root.transform);
            GameClock clock = CreateClock(root.transform, player);
            clock.Bind(session.ConfigureTime(
                _environmentSettings.RealSecondsPerGameDay, _environmentSettings.RescueDay));
            root.AddComponent<DayNightEnvironment>().Bind(clock, sun, _environmentSettings);
            GameObject hud = CreateHud(root.transform, player, clock, _inventorySkin);
            hud.AddComponent<MinimapView>().Bind(player.transform, camera, session.Exploration);
            CreateBuildingSystem(root.transform, player, camera, hud, session, clock);
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

        /// <summary>
        /// Lays the patched terrain over the base ground disc and gives the tiles within reach of the
        /// player something to dig. The tile map itself belongs to the expedition, so terrain the player
        /// already broke does not grow back while they are inside the pod.
        /// </summary>
        private void CreateTerrainPatches(Transform parent, Transform target, GameSessionState session)
        {
            if (_terrainPatchSettings == null)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)} on '{name}' has no terrain patch settings; the surface will be bare regolith.", this);
                return;
            }

            session.Terrain.Configure(
                _terrainSettings.Seed + _terrainPatchSettings.SeedOffset, _terrainPatchSettings,
                SurfaceResourceDensityMultiplier);

            var terrainPatches = new GameObject("Terrain Patches");
            terrainPatches.transform.SetParent(parent);
            TerrainChunkStreamer streamer = terrainPatches.AddComponent<TerrainChunkStreamer>();
            streamer.Configure(_terrainPatchSettings, session.Terrain);
            streamer.SetTarget(target);
            TerrainDigSiteSpawner digSites = terrainPatches.AddComponent<TerrainDigSiteSpawner>();
            digSites.Configure(session.Terrain);
            digSites.SetTarget(target);
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
                _terrainSettings.Seed + _resourceSpawnSettings.SeedOffset, target.position, buildGrid,
                SurfaceResourceDensityMultiplier);
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
            controller.height = SurfaceRobotControllerHeight;
            controller.radius = SurfaceRobotControllerRadius;
            controller.center = Vector3.up * (SurfaceRobotControllerHeight * .5f);

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
            if (_worldVisuals != null)
            {
                PlayerAnimationController animation;
                if (_worldVisuals.HasSurfaceRobotAnimation)
                {
                    animation = PlayerAnimationController.Create(
                        player,
                        null,
                        _worldVisuals.SurfaceRobotHeight,
                        _worldVisuals.SurfaceRobotAnimationSheet,
                        _worldVisuals.SurfaceRobotFramesPerDirection,
                        _worldVisuals.SurfaceRobotFrameRects,
                        _worldVisuals.SurfaceRobotFramePivots);
                }
                else
                {
                    Debug.LogWarning("Surface robot animation is incomplete; using the cabin explorer frames.",
                        _worldVisuals);
                    animation = PlayerAnimationController.Create(
                        player,
                        _worldVisuals.PlayerSprite,
                        _worldVisuals.PlayerHeight,
                        _worldVisuals.PlayerAnimationSheet,
                        _worldVisuals.PlayerFramesPerDirection,
                        _worldVisuals.PlayerFrameRects,
                        _worldVisuals.PlayerFramePivots);
                }

                animation.ConfigureTools(_worldVisuals.PlayerToolAnimations);
            }
            else
            {
                PlayerAnimationController.Create(player, null, 1.8f);
            }

            Color shadowColor = _worldVisuals != null ? _worldVisuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(player, new Vector2(1.44f, .64f), shadowColor);
        }

        private Camera CreateCamera(Transform parent, Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            // The steep orthographic view keeps square terrain artwork nearly square on screen while
            // preserving enough depth to read cutout characters, buildings and gathering ranges.
            camera.orthographicSize = SurfaceCameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .025f, .018f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1100f;
            camera.transparencySortMode = TransparencySortMode.Orthographic;
            cameraObject.AddComponent<AudioListener>();

            FollowCamera followCamera = camera.gameObject.AddComponent<FollowCamera>();
            followCamera.SetTarget(target);
            return camera;
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
        private void CreateBuildingSystem(Transform parent, GameObject player, Camera camera, GameObject hud,
            GameSessionState session, GameClock clock)
        {
            if (_buildingCatalog == null)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)} on '{name}' has no building catalog; the build panel is unavailable.", this);
                return;
            }

            var systemObject = new GameObject("Building System");
            systemObject.transform.SetParent(parent);
            BuildGridOverlay gridOverlay = systemObject.AddComponent<BuildGridOverlay>();
            gridOverlay.Bind(session.Buildings.Grid, camera);
            BuildingPlacementController controller = systemObject.AddComponent<BuildingPlacementController>();
            ItemTransferSystem transferSystem = systemObject.AddComponent<ItemTransferSystem>();
            PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
            transferSystem.Bind(session.Buildings, gridOverlay, hud.GetComponent<ItemTransferPostView>(),
                player.transform, controller, camera);
            controller.Bind(session.Buildings, playerInventory, _buildingCatalog, _worldVisuals, session,
                hud.GetComponent<CookingView>(), clock, hud.GetComponent<MiningDrillView>(),
                hud.GetComponent<PlanterBoxView>(), transferSystem);

            hud.AddComponent<CraftingDrawerView>().Bind(
                controller,
                playerInventory,
                _craftingCatalog,
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
            hudObject.AddComponent<MiningDrillView>();
            hudObject.AddComponent<PlanterBoxView>();
            hudObject.AddComponent<ItemTransferPostView>();
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
