using PlanetSurvival.Bootstrap;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.Water.Domain;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Interiors;
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

        // A 32m chunk carries 0.75 nodes on average, about one node per 1350m². The surface is meant to feel
        // barren, so a walk of a few chunks yields a single rock or debris pile rather than a field of them.
        private const float ResourceChunkSize = 32f;
        private const int ResourceLoadRadiusInChunks = 2;
        private const float ResourceMinimumSpacing = 8f;

        // Wide enough that the largest node plus the player capsule cannot overlap the start position.
        private const float ResourceSpawnClearanceRadius = 3f;
        private const string ScenesDirectory = "Assets/Game/Scenes";
        private const string ConfigurationDirectory = "Assets/Game/Configuration";
        private const string TerrainSettingsPath = ConfigurationDirectory + "/DefaultTerrainSettings.asset";
        private const string EnvironmentSettingsPath = ConfigurationDirectory + "/DefaultEnvironmentSettings.asset";
        private const string ResourceSpawnSettingsPath = ConfigurationDirectory + "/DefaultResourceSpawnSettings.asset";
        private const string EnergyBarPath = ConfigurationDirectory + "/EnergyBar.asset";
        private const string PotatoPath = ConfigurationDirectory + "/Potato.asset";
        private const string AluminumAlloyPath = ConfigurationDirectory + "/AluminumAlloy.asset";
        private const string RoastPotatoPath = ConfigurationDirectory + "/RoastPotato.asset";
        private const string RoastPotatoRecipePath = ConfigurationDirectory + "/RoastPotatoRecipe.asset";
        private const string OvenStationPath = ConfigurationDirectory + "/OvenStation.asset";
        private const string BuildingCatalogPath = ConfigurationDirectory + "/DefaultBuildingCatalog.asset";
        private const string IceChunkPath = ConfigurationDirectory + "/IceChunk.asset";
        private const string PotatoCropPath = ConfigurationDirectory + "/PotatoCrop.asset";

        // The ice-water-food loop. One chunk yields one litre, one planting drinks 1.5 L and returns
        // four potatoes for one seed, so two trays feed one explorer and still leave water to drink.
        // Together they decide how often the surface has to be visited: roughly one ice run every
        // five days, which is the pace the suit and the backpack are sized for.
        private const int PotatoGrowthGameHours = 20;
        private const int PotatoPlantingWaterMilliliters = 1500;
        private const int PotatoHarvestQuantity = 4;

        // Ice sits in the open, so it is the one resource a stranded explorer can always work towards.
        private const float IceGatherSeconds = 4f;
        private const int IceChunksPerDeposit = 3;
        private const float IceNodesPerChunk = .4f;

        // Starter structures. The costs are deliberately small: the first shelter should be reachable from
        // one gathering trip, so the placement grid is learned long before resources become a constraint.
        private const float StoneWallSeconds = 6f;
        private const float MetalBarricadeSeconds = 8f;
        private const float FieldOvenSeconds = 20f;

        // Roasting one potato takes a bit over an in-game hour at the default day length: long enough
        // that the player leaves the oven and does something else, short enough to stay a routine chore.
        private const float RoastPotatoSeconds = 30f;
        private const int RoastPotatoCalories = 300;
        private const string OvenConditionId = "station.oven";
        private const string BootstrapScenePath = ScenesDirectory + "/Bootstrap.unity";
        private const string MainMenuScenePath = ScenesDirectory + "/MainMenu.unity";
        private const string GameplayScenePath = ScenesDirectory + "/Gameplay.unity";
        private const string LandingPodHabitatScenePath = ScenesDirectory + "/LandingPodHabitat.unity";
        private const string LandingPodCargoScenePath = ScenesDirectory + "/LandingPodCargo.unity";

        [MenuItem("Planet Survival/Setup Formal Scenes")]
        public static void CreateOrUpdate()
        {
            EnsureDirectory("Assets/Game", "Scenes");
            EnsureDirectory("Assets/Game", "Configuration");

            TerrainGenerationSettings settings = GetOrCreateTerrainSettings();
            PlanetEnvironmentSettings environmentSettings = GetOrCreateEnvironmentSettings();
            InventorySkin inventorySkin = UiArtSetup.GetOrCreateInventorySkin();
            WorldVisualSettings worldVisuals = WorldArtSetup.GetOrCreateWorldVisualSettings();
            ItemDefinition energyBar = GetOrCreateEnergyBar();
            ItemDefinition potato = GetOrCreatePotato();
            ItemDefinition aluminumAlloy = GetOrCreateAluminumAlloy();
            ItemDefinition iceChunk = GetOrCreateIceChunk();
            CropDefinition potatoCrop = GetOrCreatePotatoCrop(potato);
            ResourceSpawnSettings resourceSpawnSettings = GetOrCreateResourceSettings(iceChunk);
            CookingStationDefinition oven = GetOrCreateOven(potato);
            BuildingCatalog buildingCatalog = GetOrCreateBuildingCatalog(oven);
            WorldArtSetup.AssignResourceSprites();
            WorldArtSetup.ConfigureInteriorPropSprites();
            UiArtSetup.AssignItemIcons();
            CreateBootstrapScene(energyBar, potato, aluminumAlloy);
            CreateMainMenuScene();
            CreateLandingPodScene(LandingPodDeck.Habitat, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodHabitatScenePath);
            CreateLandingPodScene(LandingPodDeck.Cargo, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodCargoScenePath);
            CreateGameplayScene(settings, environmentSettings, resourceSpawnSettings, inventorySkin, worldVisuals,
                buildingCatalog);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Planet Survival formal scenes and default configuration are ready.");
        }

        private static ResourceSpawnSettings GetOrCreateResourceSettings(ItemDefinition iceChunk)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition scrap = GetOrCreateItem("MetalScrap", "metal_scrap", "Metal Scrap", 2, 10);

            ResourceNodeDefinition rock = GetOrCreateNode("RockNode", "rock", "Rock", 2.5f,
                new Vector3(.9f, 1.1f, .72f), new ResourceYield(stone, 2));
            ResourceNodeDefinition debris = GetOrCreateNode("DebrisNode", "debris", "Debris", 3.5f,
                new Vector3(1f, 1f, .8f), new ResourceYield(scrap, 1));
            // Compose the authored square decal instead of stretching it. Repeated entries weight the mix:
            // small remnants remain, but most deposits read as substantial connected sheets.
            ResourceNodeDefinition iceDeposit = GetOrCreateNode("IceDepositNode", "ice_deposit", "Ice Deposit",
                IceGatherSeconds, new Vector3(1.1f, .15f, 1.1f), new ResourceYield(iceChunk, IceChunksPerDeposit));
            // The sheet lies flat on the ground: the explorer walks over it rather than around it.
            iceDeposit.ConfigureCollision(false);
            iceDeposit.ConfigurePresentation(ResourceVisualMode.GroundDecal);
            iceDeposit.ConfigureBlobShadow(false);
            iceDeposit.ConfigureGroundPatchFootprints(
                Vector2Int.one,
                new Vector2Int(2, 2), new Vector2Int(2, 2), new Vector2Int(2, 2),
                new Vector2Int(2, 3), new Vector2Int(3, 2),
                new Vector2Int(3, 3));
            EditorUtility.SetDirty(iceDeposit);

            ResourceSpawnSettings settings = AssetDatabase.LoadAssetAtPath<ResourceSpawnSettings>(ResourceSpawnSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ResourceSpawnSettings>();
                settings.name = "Default Resource Spawn Settings";
                AssetDatabase.CreateAsset(settings, ResourceSpawnSettingsPath);
            }

            settings.Configure(ResourceSeedOffset, ResourceChunkSize, ResourceLoadRadiusInChunks,
                ResourceMinimumSpacing, ResourceSpawnClearanceRadius,
                new ResourceSpawnEntry(rock, 0.5f),
                new ResourceSpawnEntry(debris, 0.25f),
                new ResourceSpawnEntry(iceDeposit, IceNodesPerChunk));
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

        private static ItemDefinition GetOrCreateEnergyBar()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(EnergyBarPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Energy Bar";
                AssetDatabase.CreateAsset(item, EnergyBarPath);
            }

            item.Configure(
                "energy_bar",
                "Energy Bar",
                1,
                GameSessionState.InitialEnergyBarCount,
                true,
                true,
                new VitalEffect(VitalType.Sanity, 6f));
            item.ConfigureNutrition(500);
            item.ConfigureDescription("500 kcal emergency ration. Restores 20 hunger and 6 sanity.");
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// Surface ice, the only renewable source of water. It carries no nutrition on purpose: ground
        /// ice is not drinkable, so a haul is worth nothing until the cargo-deck processor has run it.
        /// </summary>
        private static ItemDefinition GetOrCreateIceChunk()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(IceChunkPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Ice Chunk";
                AssetDatabase.CreateAsset(item, IceChunkPath);
            }

            item.Configure("ice_chunk", "Ice Chunk", 1, 20, false, true);
            item.ConfigureDescription(
                "Water ice cut from a surface deposit, laced with perchlorate salts and dust. The cargo-deck " +
                $"processor filters one chunk into {WaterProcessor.MillilitersPerIceChunk} mL of drinking water.");
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The one crop the habitat rack grows. A planting returns more potatoes than it sows, which is
        /// what turns a finite landing stock into an expedition that can last to the rescue day.
        /// </summary>
        private static CropDefinition GetOrCreatePotatoCrop(ItemDefinition potato)
        {
            CropDefinition crop = AssetDatabase.LoadAssetAtPath<CropDefinition>(PotatoCropPath);
            if (crop == null)
            {
                crop = ScriptableObject.CreateInstance<CropDefinition>();
                crop.name = "Potato Crop";
                AssetDatabase.CreateAsset(crop, PotatoCropPath);
            }

            crop.Configure("potato_crop", "Potato", potato, 1, potato, PotatoHarvestQuantity,
                PotatoGrowthGameHours, PotatoPlantingWaterMilliliters);
            EditorUtility.SetDirty(crop);
            return crop;
        }

        private static ItemDefinition GetOrCreatePotato()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotatoPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Potato";
                AssetDatabase.CreateAsset(item, PotatoPath);
            }

            item.Configure("potato", "Potato", 1, 20, false, true);
            item.ConfigureDescription(
                "A raw vegetable ingredient. Roast it in an oven to eat it, or plant it in the habitat " +
                "hydroponics rack to grow more.");
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The salvaged hull plating the pod lands with. It is the only refined metal available before the
        /// player finds a way to smelt more, so the whole starting stock ships in cargo storage.
        /// </summary>
        private static ItemDefinition GetOrCreateAluminumAlloy()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AluminumAlloyPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Aluminum Alloy";
                AssetDatabase.CreateAsset(item, AluminumAlloyPath);
            }

            item.Configure("aluminum_alloy", "Aluminum Alloy", 1, GameSessionState.InitialAluminumAlloyCount,
                false, true);
            item.ConfigureDescription("A light structural alloy sheet salvaged from the pod hull.");
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The habitat galley range and the dishes it serves. Recipes stay separate assets so a second
        /// station can offer the same dish once more cooking spots exist.
        /// </summary>
        private static CookingStationDefinition GetOrCreateOven(ItemDefinition potato)
        {
            ItemDefinition roastPotato = GetOrCreateRoastPotato();
            CraftingRecipe roastPotatoRecipe = GetOrCreateRecipe(
                RoastPotatoRecipePath,
                "roast_potato",
                "Roast Potato",
                new[] { new CraftingItemAmount(potato, 1) },
                new[] { new CraftingItemAmount(roastPotato, 1) },
                RoastPotatoSeconds,
                OvenConditionId);

            CookingStationDefinition oven = AssetDatabase.LoadAssetAtPath<CookingStationDefinition>(OvenStationPath);
            if (oven == null)
            {
                oven = ScriptableObject.CreateInstance<CookingStationDefinition>();
                oven.name = "Oven";
                AssetDatabase.CreateAsset(oven, OvenStationPath);
            }

            oven.Configure("oven", "Oven", new[] { OvenConditionId }, roastPotatoRecipe);
            EditorUtility.SetDirty(oven);
            return oven;
        }

        /// <summary>
        /// The structures the surface build panel offers. They are separate assets so a catalog can be
        /// reshuffled — or a second catalog written for another biome — without touching the buildables.
        /// </summary>
        private static BuildingCatalog GetOrCreateBuildingCatalog(CookingStationDefinition oven)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition scrap = GetOrCreateItem("MetalScrap", "metal_scrap", "Metal Scrap", 2, 10);

            BuildableDefinition wall = GetOrCreateBuildable("StoneWallBuildable", "stone_wall", "Stone Wall",
                Vector2Int.one, StoneWallSeconds, 1.5f, new Color(.58f, .5f, .44f),
                "A stacked regolith block. Cheap cover against the wind.",
                new CraftingItemAmount(stone, 4));
            BuildableDefinition barricade = GetOrCreateBuildable("MetalBarricadeBuildable", "metal_barricade",
                "Metal Barricade", new Vector2Int(2, 1), MetalBarricadeSeconds, 1.1f, new Color(.62f, .66f, .72f),
                "A welded hull panel, two cells wide.",
                new CraftingItemAmount(scrap, 3));
            BuildableDefinition fieldOven = GetOrCreateBuildable("FieldOvenBuildable", "field_oven", "Field Oven",
                new Vector2Int(2, 2), FieldOvenSeconds, 1.6f, new Color(.72f, .44f, .26f),
                "An outdoor range. Cooks the same dishes as the galley oven.",
                new CraftingItemAmount(scrap, 6), new CraftingItemAmount(stone, 6));
            fieldOven.ConfigureCookingStation(oven);
            EditorUtility.SetDirty(fieldOven);

            BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(BuildingCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
                catalog.name = "Default Building Catalog";
                AssetDatabase.CreateAsset(catalog, BuildingCatalogPath);
            }

            catalog.Configure(wall, barricade, fieldOven);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static BuildableDefinition GetOrCreateBuildable(string assetName, string buildableId,
            string displayName, Vector2Int footprint, float buildSeconds, float worldHeight, Color bodyColor,
            string description, params CraftingItemAmount[] cost)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            BuildableDefinition buildable = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(path);
            if (buildable == null)
            {
                buildable = ScriptableObject.CreateInstance<BuildableDefinition>();
                buildable.name = displayName;
                AssetDatabase.CreateAsset(buildable, path);
            }

            buildable.Configure(buildableId, displayName, footprint, buildSeconds, cost);
            // Any artwork an artist has already assigned is kept; only the placeholder block is re-derived.
            buildable.ConfigurePresentation(buildable.WorldSprite, worldHeight, bodyColor);
            buildable.ConfigureDescription(description);
            EditorUtility.SetDirty(buildable);
            return buildable;
        }

        private static ItemDefinition GetOrCreateRoastPotato()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(RoastPotatoPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Roast Potato";
                AssetDatabase.CreateAsset(item, RoastPotatoPath);
            }

            item.Configure("roast_potato", "Roast Potato", 1, 20, true, true,
                new VitalEffect(VitalType.Sanity, 4f));
            item.ConfigureNutrition(RoastPotatoCalories);
            item.ConfigureDescription(
                $"Oven-roasted in the habitat galley. {RoastPotatoCalories} kcal, restores " +
                $"{RoastPotatoCalories / NutritionBalance.CaloriesPerHungerPoint:0} hunger and 4 sanity.");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static CraftingRecipe GetOrCreateRecipe(string path, string recipeId, string displayName,
            CraftingItemAmount[] inputs, CraftingItemAmount[] outputs, float durationSeconds,
            params string[] requiredConditionIds)
        {
            CraftingRecipe recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
                recipe.name = displayName;
                AssetDatabase.CreateAsset(recipe, path);
            }

            recipe.Configure(recipeId, displayName, inputs, outputs, requiredConditionIds);
            recipe.ConfigureDuration(durationSeconds);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static ResourceNodeDefinition GetOrCreateNode(string assetName, string resourceId,
            string displayName, float duration, Vector3 scale, params ResourceYield[] yields)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            ResourceNodeDefinition node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
            if (node == null)
            {
                node = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
                node.name = displayName;
                AssetDatabase.CreateAsset(node, path);
            }

            node.Configure(resourceId, displayName, duration, 2.25f, string.Empty, scale, yields);
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

        private static void CreateBootstrapScene(ItemDefinition energyBar, ItemDefinition potato,
            ItemDefinition aluminumAlloy)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Application");
            root.AddComponent<GameFlowController>().ConfigureStartingSupplies(energyBar, potato, aluminumAlloy);
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
            InventorySkin inventorySkin, WorldVisualSettings worldVisuals, BuildingCatalog buildingCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameplay = new GameObject("Gameplay");
            GameBootstrap bootstrap = gameplay.AddComponent<GameBootstrap>();
            bootstrap.Configure(settings, environmentSettings, resourceSpawnSettings);
            bootstrap.ConfigureUi(inventorySkin);
            bootstrap.ConfigureVisuals(worldVisuals);
            bootstrap.ConfigureBuilding(buildingCatalog);
            gameplay.AddComponent<PauseMenuView>();
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void CreateLandingPodScene(LandingPodDeck deck,
            PlanetEnvironmentSettings environmentSettings, InventorySkin inventorySkin,
            WorldVisualSettings worldVisuals, CookingStationDefinition oven, CropDefinition potatoCrop,
            ItemDefinition iceChunk, string scenePath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var landingPod = new GameObject(deck == LandingPodDeck.Habitat
                ? "Landing Pod Habitat"
                : "Landing Pod Cargo");
            LandingPodBootstrap bootstrap = landingPod.AddComponent<LandingPodBootstrap>();
            bootstrap.Configure(deck, environmentSettings, worldVisuals, inventorySkin);
            bootstrap.ConfigureCooking(oven);
            bootstrap.ConfigureLifeSupport(potatoCrop, iceChunk);
            landingPod.AddComponent<PauseMenuView>();
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(LandingPodHabitatScenePath, true),
                new EditorBuildSettingsScene(LandingPodCargoScenePath, true),
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
