using PlanetSurvival.Bootstrap;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
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
            CreateBootstrapScene();
            CreateMainMenuScene();
            CreateGameplayScene(settings, environmentSettings, resourceSpawnSettings);
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
                new Vector3(1.2f, .8f, 1f), new ResourceYield(stone, 2));
            ResourceNodeDefinition debris = GetOrCreateNode("DebrisNode", "debris", "Debris", 3.5f,
                new Color(.38f, .42f, .46f), new Vector3(1.1f, .45f, 1.4f), new ResourceYield(scrap, 1));
            ResourceNodeDefinition plant = GetOrCreateNode("PlantNode", "plant", "Alien Plant", 1.5f,
                new Color(.24f, .7f, .32f), new Vector3(.55f, 1.2f, .55f), new ResourceYield(fiber, 2));

            ResourceSpawnSettings settings = AssetDatabase.LoadAssetAtPath<ResourceSpawnSettings>(ResourceSpawnSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ResourceSpawnSettings>();
                settings.name = "Default Resource Spawn Settings";
                AssetDatabase.CreateAsset(settings, ResourceSpawnSettingsPath);
            }

            settings.Configure(7919, 3f, new ResourceSpawnEntry(rock, 12), new ResourceSpawnEntry(debris, 8),
                new ResourceSpawnEntry(plant, 14));
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
                return settings;
            }

            settings = ScriptableObject.CreateInstance<TerrainGenerationSettings>();
            settings.name = "Default Terrain Settings";
            settings.Configure(24, 24, 1f, 2f, 8128);
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
            PlanetEnvironmentSettings environmentSettings, ResourceSpawnSettings resourceSpawnSettings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameplay = new GameObject("Gameplay");
            gameplay.AddComponent<GameBootstrap>().Configure(settings, environmentSettings, resourceSpawnSettings);
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
