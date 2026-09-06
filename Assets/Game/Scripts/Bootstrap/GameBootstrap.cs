using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.UI.HUD;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Grid;
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

        private const string RuntimeRootName = "Gameplay Runtime";

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
            GridMap map = new GridTerrainGenerator().Generate(_terrainSettings);

            GridTerrainView terrainView = CreateTerrain(root.transform, map);
            ResourceNodeSpawner.Spawn(root.transform, map, _terrainSettings, _resourceSpawnSettings, _worldVisuals);
            GameObject player = CreatePlayer(root.transform, map);
            terrainView.SetTarget(player.transform);
            CreateCamera(root.transform, player.transform);
            Light sun = CreateLighting(root.transform);
            GameClock clock = CreateClock(root.transform, player);
            clock.Configure(_environmentSettings.RealSecondsPerGameDay, _environmentSettings.RescueDay);
            root.AddComponent<DayNightEnvironment>().Bind(clock, sun, _environmentSettings);
            CreateHud(root.transform, player, clock, _inventorySkin);
            BindPlayerDeath(player);
        }

        private GridTerrainView CreateTerrain(Transform parent, GridMap map)
        {
            var terrain = new GameObject("Grid Terrain");
            terrain.transform.SetParent(parent);
            GridTerrainView terrainView = terrain.AddComponent<GridTerrainView>();
            terrainView.Build(map, _terrainSettings, _worldVisuals);
            return terrainView;
        }

        private GameObject CreatePlayer(Transform parent, GridMap map)
        {
            int centerX = map.Width / 2;
            int centerZ = map.Length / 2;
            var player = new GameObject();
            player.name = "Player";
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(
                centerX * _terrainSettings.CellSize,
                0f,
                centerZ * _terrainSettings.CellSize);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.5f;
            controller.radius = .32f;
            controller.center = Vector3.up * .75f;

            player.AddComponent<PlayerSurvival>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<SurvivalDecay>();
            player.AddComponent<PlayerOxygen>();
            player.AddComponent<PlayerHypoxia>();
            player.AddComponent<PlayerInteractor>();
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
            camera.orthographic = false;
            camera.fieldOfView = 90f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.003f, .006f, .012f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1100f;
            camera.transparencySortMode = TransparencySortMode.Perspective;
            cameraObject.AddComponent<AudioListener>();

            FollowCamera followCamera = camera.gameObject.AddComponent<FollowCamera>();
            followCamera.SetTarget(target);
            Sprite horizonSprite = _worldVisuals != null ? _worldVisuals.HorizonSprite : null;
            if (horizonSprite == null)
            {
                horizonSprite = Resources.Load<Sprite>("World/MartianHorizon");
            }

            if (horizonSprite == null)
            {
                Texture2D horizonTexture = Resources.Load<Texture2D>("World/MartianHorizon");
                if (horizonTexture != null)
                {
                    horizonSprite = Sprite.Create(horizonTexture,
                        new Rect(0f, 0f, horizonTexture.width, horizonTexture.height),
                        new Vector2(.5f, .5f), 100f);
                    horizonSprite.name = "Runtime Martian Horizon";
                }
            }

            HorizonBackdrop.Create(camera, horizonSprite);
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
            return clock;
        }

        private static void CreateHud(Transform parent, GameObject player, GameClock clock, InventorySkin inventorySkin)
        {
            var hudObject = new GameObject("Survival HUD");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<SurvivalHudView>().Bind(player.GetComponent<PlayerSurvival>(), clock);
            hudObject.AddComponent<InteractionPromptView>().Bind(player.GetComponent<PlayerInteractor>());
            hudObject.AddComponent<InventoryView>().Bind(player.GetComponent<PlayerInventory>(), inventorySkin);
            hudObject.AddComponent<GameOverView>();
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
