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
using UnityEngine;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TerrainGenerationSettings _terrainSettings;
        [SerializeField] private PlanetEnvironmentSettings _environmentSettings;
        [SerializeField] private ResourceSpawnSettings _resourceSpawnSettings;

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

            CreateTerrain(root.transform, map);
            ResourceNodeSpawner.Spawn(root.transform, map, _terrainSettings, _resourceSpawnSettings);
            GameObject player = CreatePlayer(root.transform, map);
            CreateCamera(root.transform, player.transform);
            Light sun = CreateLighting(root.transform);
            GameClock clock = CreateClock(root.transform, player.GetComponent<SurvivalDecay>());
            clock.Configure(_environmentSettings.RealSecondsPerGameDay, _environmentSettings.RescueDay);
            root.AddComponent<DayNightEnvironment>().Bind(clock, sun, _environmentSettings);
            CreateHud(root.transform, player, clock);
            BindPlayerDeath(player);
        }

        private void CreateTerrain(Transform parent, GridMap map)
        {
            var terrain = new GameObject("Grid Terrain");
            terrain.transform.SetParent(parent);
            terrain.AddComponent<GridTerrainView>().Build(map, _terrainSettings);
        }

        private GameObject CreatePlayer(Transform parent, GridMap map)
        {
            int centerX = map.Width / 2;
            int centerZ = map.Length / 2;
            GridCell spawnCell = map[centerX, centerZ];

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(
                centerX * _terrainSettings.CellSize,
                spawnCell.SurfaceHeight + 1.1f,
                centerZ * _terrainSettings.CellSize);

            Destroy(player.GetComponent<CapsuleCollider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;

            player.AddComponent<PlayerSurvival>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<SurvivalDecay>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<ThirdPersonMotor>();
            return player;
        }

        private static void CreateCamera(Transform parent, Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            var camera = cameraObject.AddComponent<Camera>();
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

        private static GameClock CreateClock(Transform parent, SurvivalDecay survivalDecay)
        {
            var clockObject = new GameObject("Game Clock");
            clockObject.transform.SetParent(parent);
            GameClock clock = clockObject.AddComponent<GameClock>();
            clock.Bind(survivalDecay);
            return clock;
        }

        private static void CreateHud(Transform parent, GameObject player, GameClock clock)
        {
            var hudObject = new GameObject("Survival HUD");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<SurvivalHudView>().Bind(player.GetComponent<PlayerSurvival>(), clock);
            hudObject.AddComponent<InteractionPromptView>().Bind(player.GetComponent<PlayerInteractor>());
            hudObject.AddComponent<InventoryView>().Bind(player.GetComponent<PlayerInventory>());
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
