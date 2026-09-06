using PlanetSurvival.Bootstrap;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.World.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetSurvival.Editor
{
    public static class ProjectSceneSetup
    {
        private const int DefaultMapWidth = 48;
        private const int DefaultMapLength = 48;
        private const int ResourceSeedOffset = 7919;

        // A 32m chunk carries roughly 6.5 nodes on average, about one node per 150m² instead of the previous
        // one per 17m² inside the single starting area.
        private const float ResourceChunkSize = 32f;
        private const int ResourceLoadRadiusInChunks = 2;
        private const float ResourceMinimumSpacing = 4f;

        // Wide enough that the largest node plus the player capsule cannot overlap the start position.
        private const float ResourceSpawnClearanceRadius = 3f;
        private const string ScenesDirectory = "Assets/Game/Scenes";
        private const string ConfigurationDirectory = "Assets/Game/Configuration";
        private const string TerrainSettingsPath = ConfigurationDirectory + "/DefaultTerrainSettings.asset";
        private const string EnvironmentSettingsPath = ConfigurationDirectory + "/DefaultEnvironmentSettings.asset";
        private const string ResourceSpawnSettingsPath = ConfigurationDirectory + "/DefaultResourceSpawnSettings.asset";
        private const string BootstrapScenePath = ScenesDirectory + "/Bootstrap.unity";
        private const string MainMenuScenePath = ScenesDirectory + "/MainMenu.unity";
        private const string GameplayScenePath = ScenesDirectory + "/Gameplay.unity";

        [MenuItem("Planet Survival/Setup Formal Scenes")]
        public static void CreateOrUpdate()
        {
            EnsureDirectory("Assets/Game", "Scenes");
            EnsureDirectory("Assets/Game", "Configuration");

            TerrainGenerationSettings settings = GetOrCreateTerrainSettings();
            PlanetEnvironmentSettings environmentSettings = GetOrCreateEnvironmentSettings();
            ResourceSpawnSettings resourceSpawnSettings = GetOrCreateResourceSettings();
            InventorySkin inventorySkin = UiArtSetup.GetOrCreateInventorySkin();
            WorldVisualSettings worldVisuals = WorldArtSetup.GetOrCreateWorldVisualSettings();
            WorldArtSetup.AssignResourceSprites();
            UiArtSetup.AssignItemIcons();
            CreateBootstrapScene();
            CreateMainMenuScene();
            CreateGameplayScene(settings, environmentSettings, resourceSpawnSettings, inventorySkin, worldVisuals);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Planet Survival formal scenes and default configuration are ready.");
        }

        private static ResourceSpawnSettings GetOrCreateResourceSettings()
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition scrap = GetOrCreateItem("MetalScrap", "metal_scrap", "Metal Scrap", 2, 10);
            ItemDefinition fiber = GetOrCreateItem("PlantFiber", "plant_fiber", "Plant Fiber", 1, 20);

            ResourceNodeDefinition rock = GetOrCreateNode("RockNode", "rock", "Rock", 2.5f, Color.gray,
                new Vector3(.9f, 1.1f, .72f), new ResourceYield(stone, 2));
            ResourceNodeDefinition debris = GetOrCreateNode("DebrisNode", "debris", "Debris", 3.5f,
                new Color(.38f, .42f, .46f), new Vector3(1f, 1f, .8f), new ResourceYield(scrap, 1));
            ResourceNodeDefinition plant = GetOrCreateNode("PlantNode", "plant", "Alien Plant", 1.5f,
                new Color(.24f, .7f, .32f), new Vector3(.6f, 1.05f, .55f), new ResourceYield(fiber, 2));

            ResourceSpawnSettings settings = AssetDatabase.LoadAssetAtPath<ResourceSpawnSettings>(ResourceSpawnSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ResourceSpawnSettings>();
                settings.name = "Default Resource Spawn Settings";
                AssetDatabase.CreateAsset(settings, ResourceSpawnSettingsPath);
            }

            settings.Configure(ResourceSeedOffset, ResourceChunkSize, ResourceLoadRadiusInChunks,
                ResourceMinimumSpacing, ResourceSpawnClearanceRadius,
                new ResourceSpawnEntry(rock, 2.5f),
                new ResourceSpawnEntry(debris, 1.5f),
                new ResourceSpawnEntry(plant, 2.5f));
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static ItemDefinition GetOrCreateItem(string assetName, string itemId, string displayName,
            int capacity, int stackSize)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = displayName;
                AssetDatabase.CreateAsset(item, path);
            }

            item.Configure(itemId, displayName, capacity, stackSize, false, true);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ResourceNodeDefinition GetOrCreateNode(string assetName, string resourceId,
            string displayName, float duration, Color color, Vector3 scale, params ResourceYield[] yields)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            ResourceNodeDefinition node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
            if (node == null)
            {
                node = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
                node.name = displayName;
                AssetDatabase.CreateAsset(node, path);
            }

            node.Configure(resourceId, displayName, duration, 2.25f, string.Empty, color, scale, yields);
            EditorUtility.SetDirty(node);
            return node;
        }

        private static PlanetEnvironmentSettings GetOrCreateEnvironmentSettings()
        {
            PlanetEnvironmentSettings settings = AssetDatabase.LoadAssetAtPath<PlanetEnvironmentSettings>(EnvironmentSettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<PlanetEnvironmentSettings>();
            settings.name = "Default Environment Settings";
            settings.ConfigureDefaults();
            AssetDatabase.CreateAsset(settings, EnvironmentSettingsPath);
            return settings;
        }

        private static TerrainGenerationSettings GetOrCreateTerrainSettings()
        {
            TerrainGenerationSettings settings = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(TerrainSettingsPath);
            if (settings != null)
            {
                settings.Configure(DefaultMapWidth, DefaultMapLength, 1f, 8128);
                EditorUtility.SetDirty(settings);
                return settings;
            }

            settings = ScriptableObject.CreateInstance<TerrainGenerationSettings>();
            settings.name = "Default Terrain Settings";
            settings.Configure(DefaultMapWidth, DefaultMapLength, 1f, 8128);
            AssetDatabase.CreateAsset(settings, TerrainSettingsPath);
            return settings;
        }

        private static void CreateBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Application");
            root.AddComponent<GameFlowController>();
            root.AddComponent<BootstrapSceneEntry>();
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Main Menu").AddComponent<MainMenuView>();
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateGameplayScene(TerrainGenerationSettings settings,
            PlanetEnvironmentSettings environmentSettings, ResourceSpawnSettings resourceSpawnSettings,
            InventorySkin inventorySkin, WorldVisualSettings worldVisuals)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameplay = new GameObject("Gameplay");
            GameBootstrap bootstrap = gameplay.AddComponent<GameBootstrap>();
            bootstrap.Configure(settings, environmentSettings, resourceSpawnSettings);
            bootstrap.ConfigureUi(inventorySkin);
            bootstrap.ConfigureVisuals(worldVisuals);
            gameplay.AddComponent<PauseMenuView>();
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };
        }

        private static void EnsureDirectory(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
