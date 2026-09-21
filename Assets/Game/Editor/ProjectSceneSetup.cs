using System.Collections.Generic;
using PlanetSurvival.Bootstrap;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Definitions;
using PlanetSurvival.Player.Animation;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI.Menu;
using PlanetSurvival.Water.Domain;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Ground;
using PlanetSurvival.World.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetSurvival.Editor
{
    public static class ProjectSceneSetup
    {
        private static readonly Vector2 DefaultStartingAreaSize = new(48f, 48f);
        private const int ResourceSeedOffset = 7919;

        // A 32m chunk carries 0.12 nodes on average, about one node per 8530m². Stone and scrap are
        // optional finds; ice is represented by the diggable terrain layer instead of a resource node.
        private const float ResourceChunkSize = 32f;
        private const int ResourceLoadRadiusInChunks = 2;
        private const float ResourceMinimumSpacing = 8f;
        private const float RockNodesPerChunk = .08f;
        private const float DebrisNodesPerChunk = .04f;

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
        private const string RawStonePath = ConfigurationDirectory + "/RawStone.asset";
        private const string EntanglementRelayCorePath =
            ConfigurationDirectory + "/EntanglementRelayCore.asset";
        private const string ChlorateSaltPath = ConfigurationDirectory + "/ChlorateSalt.asset";
        private const string IronOrePath = ConfigurationDirectory + "/IronOre.asset";
        private const string GravelPath = ConfigurationDirectory + "/Gravel.asset";
        private const string PetroleumPath = ConfigurationDirectory + "/PetroleumCanister.asset";
        private const string MiningDrillPath = ConfigurationDirectory + "/IronMiningDrill.asset";
        private const string GravelExtractorPath = ConfigurationDirectory + "/GravelExtractor.asset";
        private const string OxygenCandlePath = ConfigurationDirectory + "/OxygenCandle.asset";
        private const string OxygenCandleRecipePath = ConfigurationDirectory + "/OxygenCandleRecipe.asset";
        private const string RoastPotatoPath = ConfigurationDirectory + "/RoastPotato.asset";
        private const string RoastPotatoRecipePath = ConfigurationDirectory + "/RoastPotatoRecipe.asset";
        private const string OvenStationPath = ConfigurationDirectory + "/OvenStation.asset";
        private const string BuildingCatalogPath = ConfigurationDirectory + "/DefaultBuildingCatalog.asset";
        private const string IceChunkPath = ConfigurationDirectory + "/IceChunk.asset";
        private const string PotatoCropPath = ConfigurationDirectory + "/PotatoCrop.asset";
        private const string SoilPath = ConfigurationDirectory + "/Soil.asset";
        private const string PlasticSheetPath = ConfigurationDirectory + "/PlasticSheet.asset";
        private const string CarbonDioxideCanisterPath = ConfigurationDirectory + "/CarbonDioxideCanister.asset";
        private const string CarbonDioxideFilterCartridgePath =
            ConfigurationDirectory + "/CarbonDioxideFilterCartridge.asset";
        private const string PlanterBoxDefinitionPath = ConfigurationDirectory + "/PlanterBox.asset";
        private const string PickaxePath = ConfigurationDirectory + "/Pickaxe.asset";
        private const string ShovelPath = ConfigurationDirectory + "/Shovel.asset";
        private const string PickaxeRecipePath = ConfigurationDirectory + "/PoweredPickaxeRecipe.asset";
        private const string CraftingCatalogPath = ConfigurationDirectory + "/DefaultCraftingCatalog.asset";
        private const string PickaxeVisualPath = ConfigurationDirectory + "/PickaxeVisual.asset";
        private const string PickaxeSwingPath = ConfigurationDirectory + "/PickaxeSwing.asset";
        private const string PickaxeAnimationPath = ConfigurationDirectory + "/PickaxeAnimation.asset";
        private const string ShovelVisualPath = ConfigurationDirectory + "/ShovelVisual.asset";
        private const string ShovelDigPath = ConfigurationDirectory + "/ShovelDig.asset";
        private const string ShovelAnimationPath = ConfigurationDirectory + "/ShovelAnimation.asset";
        private const string TerrainPatchSettingsPath = ConfigurationDirectory + "/DefaultTerrainPatches.asset";
        private const string TerrainBlendShaderPath = "Assets/Game/Shaders/TerrainBlend.shader";
        private static readonly string[] IceTextureNames = { "IceVariant1", "IceVariant2", "IceVariant3" };
        private static readonly string[] IronTextureNames = { "IronVariant1", "IronVariant2", "IronVariant3" };

        // Diggable terrain. The three ordinary rock grades pay one stone per swing at the same rate;
        // iron is both slower and harder, so finding a deposit is a deliberate mining stop rather than a
        // roadside top-up.
        private const int TerrainSeedOffset = 5231;
        private const float TerrainTileSize = 2.75f;
        private const int TerrainChunkSizeInTiles = 8;
        private const int TerrainLoadRadiusInChunks = 1;
        private const int TerrainControlMapResolution = 128;
        private const float TerrainBlendDistance = 1.2f;
        // Legacy world-space repeat sizes kept in authored assets for backward compatibility. The current
        // tile-aligned renderer always fits one complete texture into one gameplay tile.
        private const float RockTextureTileSize = 8f;
        private const float IronTextureTileSize = 5f;
        private const float IceTextureTileSize = 12f;
        private const float RockDigSeconds = 1.6f;
        private const int RockStonePerDig = 1;
        private const float IronDigSeconds = 3.2f;
        private const int IronDigCount = 7;
        private const int IronOrePerDig = 1;
        private const float IceDigSeconds = 2.2f;
        private const int IceDigCount = 2;
        private const int IceChunksPerDig = 1;
        private const float RegolithDigSeconds = 6f;
        private const int GravelPerDig = 1;

        // Patch sizes. Coverage alone does not decide whether a grade arrives as a place or as specks:
        // the rarer a layer is, the wider its patches must be to stay whole. Iron is the extreme case at
        // one percent — at the rock grades' width its deposits came out around four tiles, ninety seconds
        // of mining after a long walk, which is not the deliberate stop it is meant to be. At this width
        // a deposit runs about twenty tiles, so it stays worth returning to across several backpack loads.
        private const float IronPatchSize = 42f;
        private const float IcePatchSize = 40f;
        private const float BoulderFieldPatchSize = 34f;
        private const float BrokenRockPatchSize = 26f;
        private const float LooseScreePatchSize = 30f;

        // Approximate share of the surface each grade may cover before overlap priority is applied. These
        // are the balancing knobs: future ice, soil or gravel terrain can use the same layer type with its
        // own coverage and patch size, without knowing anything about noise thresholds.
        private const float BoulderFieldShare = .0125f;
        private const float BrokenRockShare = .025f;
        private const float LooseScreeShare = .05f;
        private const float IronShare = .01f;
        private const float IceShare = .015f;

        // The ice-water-food loop. One chunk yields one litre, one planting drinks 1.5 L and returns
        // four potatoes for one seed, so two trays feed one explorer and still leave water to drink.
        // Together they decide how often the surface has to be visited: roughly one ice run every
        // five days, which is the pace the suit and the backpack are sized for.
        private const int PotatoGrowthGameHours = 20;
        private const int PotatoPlantingWaterMilliliters = 1500;
        private const int PotatoHarvestQuantity = 4;

        // Starter structures. The costs are deliberately small: the first shelter should be reachable from
        // one gathering trip, so the placement grid is learned long before resources become a constraint.
        private const float StoneWallSeconds = 6f;
        private const float MetalBarricadeSeconds = 8f;
        private const float FieldOvenSeconds = 20f;
        private const float OxygenCandlePlacementSeconds = 1f;
        private const float IronMiningDrillBuildSeconds = 15f;
        private const float IronMiningDrillProductionPerSecond = .25f;
        private const int IronMiningDrillOreCapacity = 20;
        private const float IronMiningDrillOutputPerSecond = 2f;
        private const float IronMiningDrillElectricityPerOre = 5f;
        private const float IronMiningDrillElectricityCapacity = 25f;
        private const float PetroleumPerCanister = 5f;
        private const float IronMiningDrillPetroleumPerOre = 1f;
        private const float IronMiningDrillPetroleumCapacity = 20f;
        private const float GravelExtractorBuildSeconds = 12f;
        private const float GravelExtractorProductionPerSecond = .1f;
        private const int GravelExtractorCapacity = 30;
        private const float GravelExtractorOutputPerSecond = 3f;
        private const float GravelExtractorElectricityPerItem = 2f;
        private const float GravelExtractorElectricityCapacity = 30f;
        private const float GravelExtractorPetroleumPerItem = .5f;
        private const float GravelExtractorPetroleumCapacity = 20f;
        private const float PlanterBoxBuildSeconds = 10f;
        private const float ItemTransferPostBuildSeconds = 4f;
        private const float SolarPanelBuildSeconds = 8f;
        private const float SolarPanelElectricityPerSecond = 2f;

        // Roasting one potato takes a bit over an in-game hour at the default day length: long enough
        // that the player leaves the oven and does something else, short enough to stay a routine chore.
        private const float RoastPotatoSeconds = 30f;
        private const int RoastPotatoCalories = 300;
        private const float OxygenCandleCraftSeconds = 45f;
        private const int ChlorateSaltPerOxygenCandle = 1;
        private const string OvenConditionId = "station.oven";
        private const string HandheldCraftingConditionId = "crafting.handheld";
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
            ItemDefinition chlorateSalt = GetOrCreateChlorateSalt();
            ItemDefinition pickaxe = GetOrCreatePickaxe();
            ItemDefinition shovel = GetOrCreateShovel();
            ItemDefinition petroleum = GetOrCreatePetroleumCanister();
            ItemDefinition soil = GetOrCreateItem("Soil", "soil", "Soil", 1, 20);
            ItemDefinition plasticSheet = GetOrCreateItem("PlasticSheet", "plastic_sheet", "Plastic Sheet", 1, 20);
            ItemDefinition carbonDioxide = GetOrCreateItem("CarbonDioxideCanister", "carbon_dioxide_canister", "CO₂ Canister", 2, 10);
            ItemDefinition carbonDioxideFilter = GetOrCreateItem(
                "CarbonDioxideFilterCartridge", "carbon_dioxide_filter_cartridge", "CO₂ Filter Cartridge", 1, 10);
            plasticSheet.ConfigureDescription("Rigid transparent plastic board for lightweight surface construction.");
            carbonDioxideFilter.ConfigureDescription("Replaceable cartridge for future carbon-dioxide filtration equipment.");
            EditorUtility.SetDirty(plasticSheet);
            EditorUtility.SetDirty(carbonDioxideFilter);
            ItemDefinition entanglementRelayCore = GetOrCreateEntanglementRelayCore();
            PlayerToolAnimationDefinition pickaxeAnimation = GetOrCreatePickaxeAnimation(pickaxe);
            PlayerToolAnimationDefinition shovelAnimation = GetOrCreateShovelAnimation(shovel);
            worldVisuals.ConfigurePlayerToolAnimations(pickaxeAnimation, shovelAnimation);
            EditorUtility.SetDirty(worldVisuals);
            ItemDefinition iceChunk = GetOrCreateIceChunk();
            CropDefinition potatoCrop = GetOrCreatePotatoCrop(potato);
            ResourceSpawnSettings resourceSpawnSettings = GetOrCreateResourceSettings(pickaxe);
            TerrainPatchSettings terrainPatchSettings = GetOrCreateTerrainPatchSettings(pickaxe, shovel, iceChunk);
            ItemDefinition oxygenCandle = GetOrCreateOxygenCandle();
            CraftingRecipe oxygenCandleRecipe = GetOrCreateOxygenCandleRecipe(
                oxygenCandle, chlorateSalt);
            CookingStationDefinition oven = GetOrCreateOven(potato, oxygenCandleRecipe);
            BuildingCatalog buildingCatalog = GetOrCreateBuildingCatalog(oven, oxygenCandle, soil, plasticSheet, carbonDioxide);
            CraftingCatalog craftingCatalog = GetOrCreateCraftingCatalog(pickaxe);
            WorldArtSetup.AssignResourceSprites();
            WorldArtSetup.ConfigureInteriorPropSprites();
            UiArtSetup.AssignItemIcons();
            CreateBootstrapScene(energyBar, potato, aluminumAlloy, chlorateSalt, pickaxe, petroleum, shovel,
                soil, plasticSheet, carbonDioxide, entanglementRelayCore, carbonDioxideFilter);
            CreateMainMenuScene();
            CreateLandingPodScene(LandingPodDeck.Habitat, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodHabitatScenePath);
            CreateLandingPodScene(LandingPodDeck.Cargo, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodCargoScenePath);
            CreateGameplayScene(settings, environmentSettings, resourceSpawnSettings, terrainPatchSettings,
                inventorySkin, worldVisuals, buildingCatalog, craftingCatalog);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Planet Survival formal scenes and default configuration are ready.");
        }

        /// <summary>
        /// Imports the supplied plastic-board and CO₂-filter artwork, creates their item definitions, and
        /// appends the solar panel to the existing building catalog without regenerating any scene.
        /// </summary>
        [MenuItem("Planet Survival/Setup Solar Content")]
        public static void SetupSolarContent()
        {
            EnsureDirectory("Assets/Game", "Configuration");
            ItemDefinition plasticSheet = GetOrCreateItem(
                "PlasticSheet", "plastic_sheet", "Plastic Sheet", 1, 20);
            ItemDefinition carbonDioxideFilter = GetOrCreateItem(
                "CarbonDioxideFilterCartridge", "carbon_dioxide_filter_cartridge", "CO₂ Filter Cartridge", 1, 10);
            plasticSheet.ConfigureDescription("Rigid transparent plastic board for lightweight surface construction.");
            carbonDioxideFilter.ConfigureDescription("Replaceable cartridge for future carbon-dioxide filtration equipment.");
            EditorUtility.SetDirty(plasticSheet);
            EditorUtility.SetDirty(carbonDioxideFilter);
            UiArtSetup.AssignItemIcons();

            BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(BuildingCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("Solar content setup requires the default building catalog. Run Setup Formal Scenes first.");
                return;
            }

            ItemDefinition aluminumAlloy = GetOrCreateAluminumAlloy();
            BuildableDefinition solarPanel = GetOrCreateBuildable(
                "SolarPanelBuildable", "solar_panel", "Solar Panel", Vector2Int.one,
                SolarPanelBuildSeconds, .15f, new Color(.12f, .2f, .34f),
                "Generates 2 electricity units per second for adjacent mining drills. Place it beside a drill.",
                new CraftingItemAmount(aluminumAlloy, 3), new CraftingItemAmount(plasticSheet, 2));
            Sprite sprite = WorldArtSetup.ImportBuildingSprite("SolarPanel");
            solarPanel.ConfigurePresentation(sprite, .15f, new Color(.12f, .2f, .34f));
            solarPanel.ConfigureIcon(sprite);
            solarPanel.ConfigureSolarPanel(true, SolarPanelElectricityPerSecond);
            EditorUtility.SetDirty(solarPanel);

            var buildables = new List<BuildableDefinition>(catalog.Buildables);
            if (!buildables.Contains(solarPanel))
            {
                buildables.Add(solarPanel);
                catalog.Configure(buildables.ToArray());
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Plastic board, CO₂ filter cartridge, and solar panel are ready.");
        }

        /// <summary>
        /// Authors the oxygen candle and adds its fabrication recipe to the existing oven without
        /// rebuilding unrelated terrain, scenes, or world configuration.
        /// </summary>
        [MenuItem("Planet Survival/Setup Oxygen Candle")]
        public static void CreateOrUpdateOxygenCandle()
        {
            ItemDefinition energyBar = AssetDatabase.LoadAssetAtPath<ItemDefinition>(EnergyBarPath);
            ItemDefinition potato = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotatoPath);
            ItemDefinition aluminumAlloy = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AluminumAlloyPath);
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            if (energyBar == null || potato == null || aluminumAlloy == null || pickaxe == null)
            {
                Debug.LogError(
                    "Oxygen candle setup requires the existing starting-cargo item assets.");
                return;
            }

            ItemDefinition chlorateSalt = GetOrCreateChlorateSalt();
            ItemDefinition oxygenCandle = GetOrCreateOxygenCandle();
            CraftingRecipe oxygenCandleRecipe = GetOrCreateOxygenCandleRecipe(
                oxygenCandle, chlorateSalt);
            CookingStationDefinition oven = GetOrCreateOven(potato, oxygenCandleRecipe);
            GetOrCreateBuildingCatalog(oven, oxygenCandle);
            ItemDefinition petroleum = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PetroleumPath);
            UpdateBootstrapStartingSupplies(energyBar, potato, aluminumAlloy, chlorateSalt, pickaxe, petroleum);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("The craftable and placeable oxygen candle is ready.");
        }

        [MenuItem("Planet Survival/Setup Iron Mining Drill")]
        public static void CreateOrUpdateIronMiningDrill()
        {
            ItemDefinition energyBar = AssetDatabase.LoadAssetAtPath<ItemDefinition>(EnergyBarPath);
            ItemDefinition potato = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotatoPath);
            ItemDefinition aluminumAlloy = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AluminumAlloyPath);
            ItemDefinition chlorateSalt = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ChlorateSaltPath);
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            ItemDefinition oxygenCandle = AssetDatabase.LoadAssetAtPath<ItemDefinition>(OxygenCandlePath);
            CookingStationDefinition oven = AssetDatabase.LoadAssetAtPath<CookingStationDefinition>(OvenStationPath);
            if (energyBar == null || potato == null || aluminumAlloy == null || chlorateSalt == null ||
                pickaxe == null || oxygenCandle == null || oven == null)
            {
                Debug.LogError("Iron mining drill setup requires the existing formal project configuration.");
                return;
            }

            ItemDefinition petroleum = GetOrCreatePetroleumCanister();
            GetOrCreateBuildingCatalog(oven, oxygenCandle);
            UpdateBootstrapStartingSupplies(
                energyBar, potato, aluminumAlloy, chlorateSalt, pickaxe, petroleum);
            UiArtSetup.AssignItemIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("The iron-terrain mining drill, petroleum fuel, and starting supply are ready.");
        }

        [MenuItem("Planet Survival/Setup Gravel Gathering")]
        public static void CreateOrUpdateGravelGathering()
        {
            ItemDefinition shovel = GetOrCreateShovel();
            ItemDefinition gravel = GetOrCreateGravel();
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            ItemDefinition iceChunk = AssetDatabase.LoadAssetAtPath<ItemDefinition>(IceChunkPath);
            ItemDefinition oxygenCandle = AssetDatabase.LoadAssetAtPath<ItemDefinition>(OxygenCandlePath);
            CookingStationDefinition oven = AssetDatabase.LoadAssetAtPath<CookingStationDefinition>(OvenStationPath);
            if (pickaxe == null || iceChunk == null || oxygenCandle == null || oven == null)
            {
                Debug.LogError("Gravel gathering setup requires the existing formal project configuration.");
                return;
            }

            PlayerToolAnimationDefinition pickaxeAnimation = GetOrCreatePickaxeAnimation(pickaxe);
            PlayerToolAnimationDefinition shovelAnimation = GetOrCreateShovelAnimation(shovel);
            WorldVisualSettings visuals = WorldArtSetup.GetOrCreateWorldVisualSettings();
            visuals.ConfigurePlayerToolAnimations(pickaxeAnimation, shovelAnimation);
            EditorUtility.SetDirty(visuals);
            GetOrCreateTerrainPatchSettings(pickaxe, shovel, iceChunk);
            GetOrCreateBuildingCatalog(oven, oxygenCandle);
            UiArtSetup.AssignItemIcons();
            UpdateBootstrapStartingSupplies(
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(EnergyBarPath),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotatoPath),
                GetOrCreateAluminumAlloy(),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(ChlorateSaltPath),
                pickaxe,
                GetOrCreatePetroleumCanister());
            EditorUtility.SetDirty(gravel);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Gravel, shovel gathering, and the powered gravel extractor are ready.");
        }

        [MenuItem("Planet Survival/Setup Planter Box Art")]
        public static void CreateOrUpdatePlanterBoxArt()
        {
            AssetDatabase.Refresh();
            BuildableDefinition buildable = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(
                ConfigurationDirectory + "/PlanterBoxBuildable.asset");
            if (buildable == null)
            {
                Debug.LogError("Planter box art setup requires the planter box configuration asset.");
                return;
            }

            Sprite sprite = WorldArtSetup.ImportBuildingSprite("PlanterBox");
            if (sprite == null)
            {
                Debug.LogError("Planter box artwork was not found in the world building art directory.");
                return;
            }

            buildable.ConfigurePresentation(sprite, 1f, new Color(.36f, .55f, .28f));
            buildable.ConfigureIcon(sprite);
            EditorUtility.SetDirty(buildable);
            AssetDatabase.SaveAssets();
            Debug.Log("Planter box world sprite and build-menu icon are ready.");
        }

        /// <summary>Authors handheld recipes and binds their catalog without rebuilding unrelated scenes.</summary>
        [MenuItem("Planet Survival/Setup Crafting Drawer")]
        public static void CreateOrUpdateCraftingDrawer()
        {
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            if (pickaxe == null)
            {
                Debug.LogError("Crafting drawer setup requires the existing pickaxe item.");
                return;
            }

            CraftingCatalog catalog = GetOrCreateCraftingCatalog(pickaxe);
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("The gameplay scene has no GameBootstrap.");
                return;
            }

            bootstrap.ConfigureCrafting(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("The left-side crafting and building drawer is configured.");
        }

        /// <summary>
        /// Reapplies only the surface-resource balance. This keeps scene authoring and unrelated assets
        /// untouched while ensuring both existing projects and future full setup runs use the same scarcity.
        /// </summary>
        [MenuItem("Planet Survival/Rebalance Sparse Surface Resources")]
        public static void RebalanceSparseSurfaceResources()
        {
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            ItemDefinition iceChunk = AssetDatabase.LoadAssetAtPath<ItemDefinition>(IceChunkPath);
            if (pickaxe == null || iceChunk == null)
            {
                Debug.LogError(
                    "Surface resource rebalance requires the existing Pickaxe and Ice Chunk assets.");
                return;
            }

            GetOrCreateResourceSettings(pickaxe);
            ItemDefinition shovel = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShovelPath);
            if (shovel == null)
            {
                Debug.LogError("Surface resource rebalance requires the existing Shovel asset.");
                return;
            }

            GetOrCreateTerrainPatchSettings(pickaxe, shovel, iceChunk);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sparse surface-resource balance is ready.");
        }

        /// <summary>
        /// Imports and wires only the iron-vein feature. This narrow setup path is useful when the formal
        /// scenes already exist: it preserves every scene and every pre-existing terrain layer verbatim.
        /// </summary>
        [MenuItem("Planet Survival/Setup Iron")]
        public static void CreateOrUpdateIron()
        {
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            TerrainPatchSettings settings =
                AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(TerrainPatchSettingsPath);
            if (pickaxe == null || settings == null)
            {
                Debug.LogError("Iron vein setup requires the existing Pickaxe and Default Terrain Patches assets.");
                return;
            }

            TerrainSurfaceDefinition iron = GetOrCreateIronSurface(pickaxe);
            var layers = new List<TerrainPatchLayer>
            {
                new(iron, IronPatchSize, IronShare, 9151)
            };
            for (int i = 0; i < settings.Layers.Count; i++)
            {
                TerrainPatchLayer layer = settings.Layers[i];
                if (layer.Surface == null || layer.Surface.TerrainId != "iron")
                {
                    layers.Add(layer);
                }
            }

            settings.Configure(settings.SeedOffset, settings.TileSize, settings.ChunkSizeInTiles,
                settings.LoadRadiusInChunks, layers.ToArray());
            ConfigureTerrainRendering(settings);
            EditorUtility.SetDirty(settings);
            UiArtSetup.AssignItemIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Iron vein terrain and item are ready.");
        }

        /// <summary>
        /// Imports and wires only the ice-layer feature. Existing terrain layers keep their authored
        /// order and values; ice is inserted just below iron so both rare deposits win overlaps with
        /// ordinary rock without changing iron's established priority.
        /// </summary>
        [MenuItem("Planet Survival/Setup Ice Terrain")]
        public static void CreateOrUpdateIceTerrain()
        {
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            ItemDefinition iceChunk = AssetDatabase.LoadAssetAtPath<ItemDefinition>(IceChunkPath);
            TerrainPatchSettings settings =
                AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(TerrainPatchSettingsPath);
            if (pickaxe == null || iceChunk == null || settings == null)
            {
                Debug.LogError(
                    "Ice terrain setup requires the existing Pickaxe, Ice Chunk and Default Terrain Patches assets.");
                return;
            }

            TerrainSurfaceDefinition ice = GetOrCreateIceSurface(pickaxe, iceChunk);
            var layers = new List<TerrainPatchLayer>();
            bool iceInserted = false;
            for (int i = 0; i < settings.Layers.Count; i++)
            {
                TerrainPatchLayer layer = settings.Layers[i];
                if (layer.Surface != null && layer.Surface.TerrainId == "ice")
                {
                    continue;
                }

                layers.Add(layer);
                if (!iceInserted && layer.Surface != null && layer.Surface.TerrainId == "iron")
                {
                    layers.Add(new TerrainPatchLayer(ice, IcePatchSize, IceShare, 6421));
                    iceInserted = true;
                }
            }

            if (!iceInserted)
            {
                layers.Insert(0, new TerrainPatchLayer(ice, IcePatchSize, IceShare, 6421));
            }

            settings.Configure(settings.SeedOffset, settings.TileSize, settings.ChunkSizeInTiles,
                settings.LoadRadiusInChunks, layers.ToArray());
            ConfigureTerrainRendering(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sparse connected ice terrain is ready.");
        }

        private static ResourceSpawnSettings GetOrCreateResourceSettings(ItemDefinition pickaxe)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition scrap = GetOrCreateItem("MetalScrap", "metal_scrap", "Metal Scrap", 2, 10);
            // Scrap is feedstock for a future furnace, not a construction-ready metal plate.
            scrap.ConfigureMaterialTags(ItemMaterialTag.None);
            scrap.ConfigureDescription(
                "Mixed wreckage recovered from surface debris. It must be smelted before it can be used in construction.");
            EditorUtility.SetDirty(scrap);

            ResourceNodeDefinition rock = GetOrCreateNode("RockNode", "rock", "Rock", 2.5f,
                pickaxe.ItemId,
                new Vector3(.9f, 1.1f, .72f), new ResourceYield(stone, 2));
            ResourceNodeDefinition debris = GetOrCreateNode("DebrisNode", "debris", "Debris", 3.5f,
                string.Empty,
                new Vector3(1f, 1f, .8f), new ResourceYield(scrap, 1));
            ResourceSpawnSettings settings = AssetDatabase.LoadAssetAtPath<ResourceSpawnSettings>(ResourceSpawnSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ResourceSpawnSettings>();
                settings.name = "Default Resource Spawn Settings";
                AssetDatabase.CreateAsset(settings, ResourceSpawnSettingsPath);
            }

            settings.Configure(ResourceSeedOffset, ResourceChunkSize, ResourceLoadRadiusInChunks,
                ResourceMinimumSpacing, ResourceSpawnClearanceRadius,
                new ResourceSpawnEntry(rock, RockNodesPerChunk),
                new ResourceSpawnEntry(debris, DebrisNodesPerChunk));
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// Authors the patched terrain: rare iron and ice deposits plus three grades of rock over the base
        /// regolith. Rare resources come first so ordinary rock cannot hide them where their fields overlap.
        /// Each layer samples its own field, so a deposit may be one tile or a connected run instead of a
        /// fixed prefab shape.
        /// </summary>
        private static TerrainPatchSettings GetOrCreateTerrainPatchSettings(
            ItemDefinition pickaxe, ItemDefinition shovel, ItemDefinition iceChunk)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition gravel = GetOrCreateGravel();

            // The artwork is a cutout layer of loose rock over the regolith, and how much of the ground it
            // hides rises with its grade: scattered gravel, then broken slabs, then solid boulders. That
            // makes a tile's hardness readable before the first swing.
            TerrainSurfaceDefinition iron = GetOrCreateIronSurface(pickaxe);
            TerrainSurfaceDefinition ice = GetOrCreateIceSurface(pickaxe, iceChunk);
            TerrainSurfaceDefinition boulderField = GetOrCreateTerrainSurface("BoulderFieldTerrain",
                "rock_boulder_field", "Boulder Field", "Stone3", 5, RockDigSeconds,
                pickaxe.ItemId, stone, RockStonePerDig, RockTextureTileSize);
            TerrainSurfaceDefinition brokenRock = GetOrCreateTerrainSurface("BrokenRockTerrain",
                "rock_broken", "Broken Rock", "Stone2", 3, RockDigSeconds,
                pickaxe.ItemId, stone, RockStonePerDig, RockTextureTileSize);
            TerrainSurfaceDefinition looseScree = GetOrCreateTerrainSurface("LooseScreeTerrain",
                "rock_loose_scree", "Loose Scree", "Stone1", 1, RockDigSeconds,
                pickaxe.ItemId, stone, RockStonePerDig, RockTextureTileSize);
            TerrainSurfaceDefinition regolith = GetOrCreateRegolithSurface(shovel, gravel);

            TerrainPatchSettings settings =
                AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(TerrainPatchSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
                settings.name = "Default Terrain Patches";
                AssetDatabase.CreateAsset(settings, TerrainPatchSettingsPath);
            }

            settings.Configure(TerrainSeedOffset, TerrainTileSize, TerrainChunkSizeInTiles,
                TerrainLoadRadiusInChunks, regolith,
                new TerrainPatchLayer(iron, IronPatchSize, IronShare, 9151),
                new TerrainPatchLayer(ice, IcePatchSize, IceShare, 6421),
                new TerrainPatchLayer(boulderField, BoulderFieldPatchSize, BoulderFieldShare, 1613),
                new TerrainPatchLayer(brokenRock, BrokenRockPatchSize, BrokenRockShare, 7817),
                new TerrainPatchLayer(looseScree, LooseScreePatchSize, LooseScreeShare, 3271));
            ConfigureTerrainRendering(settings);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static TerrainSurfaceDefinition GetOrCreateRegolithSurface(
            ItemDefinition shovel, ItemDefinition gravel)
        {
            const string path = ConfigurationDirectory + "/RegolithTerrain.asset";
            TerrainSurfaceDefinition surface = AssetDatabase.LoadAssetAtPath<TerrainSurfaceDefinition>(path);
            if (surface == null)
            {
                surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
                surface.name = "Ordinary Regolith";
                AssetDatabase.CreateAsset(surface, path);
            }

            surface.Configure("regolith", "Ordinary Regolith", 1, RegolithDigSeconds,
                shovel.ItemId, new ResourceYield(gravel, GravelPerDig));
            // Ordinary ground already comes from WorldVisualSettings; this gameplay-only surface must
            // not introduce a second texture layer merely to make the ground interactable.
            surface.ConfigureTexture(null, RockTextureTileSize);
            EditorUtility.SetDirty(surface);
            return surface;
        }

        private static void ConfigureTerrainRendering(TerrainPatchSettings settings)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TerrainBlendShaderPath);
            if (shader == null)
            {
                Debug.LogError($"Terrain blend shader was not found at '{TerrainBlendShaderPath}'.");
            }

            settings.ConfigureRendering(shader, TerrainControlMapResolution, TerrainBlendDistance);
        }

        private static TerrainSurfaceDefinition GetOrCreateIronSurface(ItemDefinition pickaxe)
        {
            ItemDefinition ironOre = GetOrCreateItem("IronOre", "iron_ore", "Iron Ore", 2, 20);
            ironOre.ConfigureDescription("Dense raw iron ore mined from rare exposed outcrops.");
            EditorUtility.SetDirty(ironOre);
            TerrainSurfaceDefinition surface = GetOrCreateTerrainSurface(
                "IronTerrain", "iron", "Iron", IronTextureNames[0],
                IronDigCount, IronDigSeconds, pickaxe.ItemId, ironOre, IronOrePerDig, IronTextureTileSize);
            var textures = new Texture2D[IronTextureNames.Length];
            for (int i = 0; i < IronTextureNames.Length; i++)
            {
                textures[i] = WorldArtSetup.ImportGroundTexture(IronTextureNames[i]);
            }

            surface.ConfigureTextureVariants(IronTextureTileSize, textures);
            EditorUtility.SetDirty(surface);
            return surface;
        }

        private static TerrainSurfaceDefinition GetOrCreateIceSurface(
            ItemDefinition pickaxe, ItemDefinition iceChunk)
        {
            TerrainSurfaceDefinition surface = GetOrCreateTerrainSurface(
                "IceTerrain", "ice", "Ice Layer", IceTextureNames[0],
                IceDigCount, IceDigSeconds, pickaxe.ItemId, iceChunk, IceChunksPerDig, IceTextureTileSize);
            var textures = new Texture2D[IceTextureNames.Length];
            for (int i = 0; i < IceTextureNames.Length; i++)
            {
                textures[i] = WorldArtSetup.ImportGroundTexture(IceTextureNames[i]);
            }

            surface.ConfigureTextureVariants(IceTextureTileSize, textures);
            EditorUtility.SetDirty(surface);
            return surface;
        }

        /// <param name="textureTileSize">
        /// Legacy world-space repeat size retained in the surface asset for compatibility. The current
        /// terrain renderer fits one complete square artwork into each gameplay tile.
        /// </param>
        private static TerrainSurfaceDefinition GetOrCreateTerrainSurface(string assetName, string terrainId,
            string displayName, string textureName, int digCount, float digDuration,
            string requiredToolItemId, ItemDefinition yieldItem, int yieldQuantity, float textureTileSize)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            TerrainSurfaceDefinition surface = AssetDatabase.LoadAssetAtPath<TerrainSurfaceDefinition>(path);
            if (surface == null)
            {
                surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
                surface.name = displayName;
                AssetDatabase.CreateAsset(surface, path);
            }

            surface.Configure(terrainId, displayName, digCount, digDuration, requiredToolItemId,
                new ResourceYield(yieldItem, yieldQuantity));
            surface.ConfigureTexture(WorldArtSetup.ImportGroundTexture(textureName), textureTileSize);
            EditorUtility.SetDirty(surface);
            return surface;
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

        private static ItemDefinition GetOrCreatePickaxe()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PickaxePath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Pickaxe";
                AssetDatabase.CreateAsset(item, PickaxePath);
            }

            Sprite sprite = WorldArtSetup.ImportPickaxeSprite();
            item.Configure("pickaxe", "Powered Pickaxe", 4, 1, false, true);
            item.ConfigureDescription("Powered field pickaxe used to break exposed rock deposits.");
            item.ConfigureIcon(sprite);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition GetOrCreateShovel()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShovelPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Shovel";
                AssetDatabase.CreateAsset(item, ShovelPath);
            }

            Sprite sprite = WorldArtSetup.ImportShovelSprite();
            item.Configure("shovel", "Shovel", 2, 1, false, true);
            item.ConfigureDescription("A hand shovel used to collect gravel from ordinary regolith.");
            item.ConfigureIcon(sprite);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition GetOrCreateGravel()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(GravelPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Gravel";
                AssetDatabase.CreateAsset(item, GravelPath);
            }

            item.Configure("gravel", "Gravel", 1, 30, false, true);
            item.ConfigureDescription("Loose mineral aggregate dug from ordinary regolith by hand or machine.");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition GetOrCreatePetroleumCanister()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PetroleumPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Petroleum Canister";
                AssetDatabase.CreateAsset(item, PetroleumPath);
            }

            item.Configure("petroleum_canister", "Petroleum Canister", 5, 10, false, true);
            item.ConfigureDescription(
                $"A sealed field-fuel canister. One canister supplies {PetroleumPerCanister:0.#} units of petroleum to compatible machines.");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static PlayerToolAnimationDefinition GetOrCreatePickaxeAnimation(ItemDefinition pickaxe)
        {
            PlayerEquipmentVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<PlayerEquipmentVisualDefinition>(PickaxeVisualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<PlayerEquipmentVisualDefinition>();
                visual.name = "Pickaxe Visual";
                AssetDatabase.CreateAsset(visual, PickaxeVisualPath);
            }

            Vector2 secondHand = new(0f, .1f);
            visual.Configure(pickaxe.Icon, .32f,
                new DirectionalEquipmentPose(new Vector2(.06f, .5f), -32f, secondHand),
                new DirectionalEquipmentPose(new Vector2(-.08f, .5f), 38f, secondHand),
                new DirectionalEquipmentPose(new Vector2(.08f, .5f), -38f, secondHand),
                new DirectionalEquipmentPose(new Vector2(.06f, .51f), 28f, secondHand, true));
            EditorUtility.SetDirty(visual);

            PlayerActionAnimationDefinition swing =
                AssetDatabase.LoadAssetAtPath<PlayerActionAnimationDefinition>(PickaxeSwingPath);
            if (swing == null)
            {
                swing = ScriptableObject.CreateInstance<PlayerActionAnimationDefinition>();
                swing.name = "Pickaxe Swing";
                AssetDatabase.CreateAsset(swing, PickaxeSwingPath);
            }

            swing.Configure(.72f, true,
                Curve(0f, 0f, .38f, -.018f, .58f, .012f, 1f, 0f),
                Curve(0f, 0f, .38f, -.025f, .58f, .01f, 1f, 0f),
                Curve(0f, 0f, .38f, -3f, .58f, 2f, 1f, 0f),
                Curve(0f, 0f, .38f, .025f, .58f, -.01f, 1f, 0f),
                Curve(0f, 0f, .18f, -.01f, .38f, .04f, .58f, .01f, 1f, 0f),
                Curve(0f, 0f, .18f, 34f, .42f, -58f, .68f, 14f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 1f));
            swing.ConfigureRig(
                Curve(0f, 0f, .18f, 35f, .42f, 15f, .68f, 25f, 1f, 0f),
                Curve(0f, 0f, .18f, 25f, .42f, 50f, .68f, 20f, 1f, 0f),
                Curve(0f, 0f, .18f, -10f, .42f, -18f, .68f, -8f, 1f, 0f),
                Curve(0f, 0f, .18f, -15f, .42f, -40f, .68f, -10f, 1f, 0f),
                Curve(0f, 0f, .18f, -25f, .42f, -15f, .68f, -25f, 1f, 0f),
                Curve(0f, 0f, .18f, 10f, .42f, 16f, .68f, 8f, 1f, 0f));
            EditorUtility.SetDirty(swing);

            PlayerToolAnimationDefinition animation =
                AssetDatabase.LoadAssetAtPath<PlayerToolAnimationDefinition>(PickaxeAnimationPath);
            if (animation == null)
            {
                animation = ScriptableObject.CreateInstance<PlayerToolAnimationDefinition>();
                animation.name = "Pickaxe Animation";
                AssetDatabase.CreateAsset(animation, PickaxeAnimationPath);
            }

            animation.Configure(pickaxe.ItemId, visual, swing);
            EditorUtility.SetDirty(animation);
            return animation;
        }

        private static PlayerToolAnimationDefinition GetOrCreateShovelAnimation(ItemDefinition shovel)
        {
            PlayerEquipmentVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<PlayerEquipmentVisualDefinition>(ShovelVisualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<PlayerEquipmentVisualDefinition>();
                visual.name = "Shovel Visual";
                AssetDatabase.CreateAsset(visual, ShovelVisualPath);
            }

            Vector2 secondHand = new(0f, .11f);
            visual.Configure(shovel.Icon, .38f,
                new DirectionalEquipmentPose(new Vector2(.06f, .48f), -28f, secondHand),
                new DirectionalEquipmentPose(new Vector2(-.08f, .48f), 34f, secondHand),
                new DirectionalEquipmentPose(new Vector2(.08f, .48f), -34f, secondHand),
                new DirectionalEquipmentPose(new Vector2(.05f, .49f), 26f, secondHand, true));
            EditorUtility.SetDirty(visual);

            PlayerActionAnimationDefinition dig =
                AssetDatabase.LoadAssetAtPath<PlayerActionAnimationDefinition>(ShovelDigPath);
            if (dig == null)
            {
                dig = ScriptableObject.CreateInstance<PlayerActionAnimationDefinition>();
                dig.name = "Shovel Dig";
                AssetDatabase.CreateAsset(dig, ShovelDigPath);
            }

            dig.Configure(.8f, true,
                Curve(0f, 0f, .45f, .01f, .7f, -.01f, 1f, 0f),
                Curve(0f, 0f, .45f, -.035f, .7f, .015f, 1f, 0f),
                Curve(0f, 0f, .45f, -4f, .7f, 2f, 1f, 0f),
                Curve(0f, 0f, .45f, .02f, .7f, -.01f, 1f, 0f),
                Curve(0f, 0f, .3f, .02f, .55f, -.035f, .78f, .01f, 1f, 0f),
                Curve(0f, 0f, .25f, 24f, .55f, -48f, .8f, 12f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 1f));
            dig.ConfigureRig(
                Curve(0f, 0f, .25f, 25f, .55f, 12f, .8f, 20f, 1f, 0f),
                Curve(0f, 0f, .25f, 20f, .55f, 42f, .8f, 15f, 1f, 0f),
                Curve(0f, 0f, .25f, -8f, .55f, -15f, .8f, -6f, 1f, 0f),
                Curve(0f, 0f, .25f, -12f, .55f, -32f, .8f, -8f, 1f, 0f),
                Curve(0f, 0f, .25f, -20f, .55f, -12f, .8f, -20f, 1f, 0f),
                Curve(0f, 0f, .25f, 8f, .55f, 14f, .8f, 6f, 1f, 0f));
            EditorUtility.SetDirty(dig);

            PlayerToolAnimationDefinition animation =
                AssetDatabase.LoadAssetAtPath<PlayerToolAnimationDefinition>(ShovelAnimationPath);
            if (animation == null)
            {
                animation = ScriptableObject.CreateInstance<PlayerToolAnimationDefinition>();
                animation.name = "Shovel Animation";
                AssetDatabase.CreateAsset(animation, ShovelAnimationPath);
            }

            animation.Configure(shovel.ItemId, visual, dig);
            EditorUtility.SetDirty(animation);
            return animation;
        }

        private static AnimationCurve Curve(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            }

            return new AnimationCurve(keys);
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
            item.ConfigureMaterialTags(ItemMaterialTag.MetalPlate);
            item.ConfigureDescription("A light structural alloy sheet salvaged from the pod hull.");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition GetOrCreateEntanglementRelayCore()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(EntanglementRelayCorePath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Entanglement Relay Core";
                AssetDatabase.CreateAsset(item, EntanglementRelayCorePath);
            }

            item.Configure("entanglement_relay_core", "Entanglement Relay Core", 3, 10, false, true);
            item.ConfigureDescription(
                "A calibrated field core that preserves a paired transport channel across distance.");
            item.ConfigureMaterialTags(ItemMaterialTag.None);
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The habitat galley range and the dishes it serves. Recipes stay separate assets so a second
        /// station can offer the same dish once more cooking spots exist.
        /// </summary>
        private static CookingStationDefinition GetOrCreateOven(
            ItemDefinition potato,
            CraftingRecipe oxygenCandleRecipe)
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

            oven.Configure("oven", "Oven", new[] { OvenConditionId },
                roastPotatoRecipe, oxygenCandleRecipe);
            EditorUtility.SetDirty(oven);
            return oven;
        }

        private static ItemDefinition GetOrCreateOxygenCandle()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(OxygenCandlePath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Oxygen Candle";
                AssetDatabase.CreateAsset(item, OxygenCandlePath);
            }

            item.Configure("oxygen_candle", "Oxygen Candle", 2, 10, false, true);
            item.ConfigureDescription(
                "A compact chlorate oxidizer block. Once placed and ignited, it burns for 24 game hours, " +
                "releasing 100 L of oxygen each game hour (2,400 L total).");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition GetOrCreateChlorateSalt()
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ChlorateSaltPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Chlorate Salt";
                AssetDatabase.CreateAsset(item, ChlorateSaltPath);
            }

            item.Configure("chlorate_salt", "Chlorate Salt", 2, 10, false, true);
            item.ConfigureDescription(
                "A sealed emergency oxidizer charge carried by the landing pod. One charge makes one oxygen candle.");
            EditorUtility.SetDirty(item);
            return item;
        }

        private static CraftingRecipe GetOrCreateOxygenCandleRecipe(
            ItemDefinition oxygenCandle,
            ItemDefinition chlorateSalt)
        {
            return GetOrCreateRecipe(
                OxygenCandleRecipePath,
                "oxygen_candle",
                "Oxygen Candle",
                new[]
                {
                    new CraftingItemAmount(chlorateSalt, ChlorateSaltPerOxygenCandle)
                },
                new[] { new CraftingItemAmount(oxygenCandle, 1) },
                OxygenCandleCraftSeconds,
                OvenConditionId);
        }

        private static CraftingCatalog GetOrCreateCraftingCatalog(ItemDefinition pickaxe)
        {
            ItemDefinition stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(RawStonePath)
                                   ?? GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition aluminumAlloy = GetOrCreateAluminumAlloy();
            CraftingRecipe pickaxeRecipe = GetOrCreateRecipe(
                PickaxeRecipePath,
                "powered_pickaxe",
                "Powered Pickaxe",
                new[]
                {
                    new CraftingItemAmount(stone, 3),
                    new CraftingItemAmount(aluminumAlloy, 2)
                },
                new[] { new CraftingItemAmount(pickaxe, 1) },
                0f,
                HandheldCraftingConditionId);

            CraftingCatalog catalog = AssetDatabase.LoadAssetAtPath<CraftingCatalog>(CraftingCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CraftingCatalog>();
                catalog.name = "Default Crafting Catalog";
                AssetDatabase.CreateAsset(catalog, CraftingCatalogPath);
            }

            catalog.Configure(new[] { HandheldCraftingConditionId }, pickaxeRecipe);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>
        /// The structures the surface build panel offers. They are separate assets so a catalog can be
        /// reshuffled — or a second catalog written for another biome — without touching the buildables.
        /// </summary>
        private static BuildingCatalog GetOrCreateBuildingCatalog(
            CookingStationDefinition oven,
            ItemDefinition oxygenCandle,
            ItemDefinition soil = null,
            ItemDefinition plasticSheet = null,
            ItemDefinition carbonDioxideCanister = null)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition aluminumAlloy = GetOrCreateAluminumAlloy();
            ItemDefinition entanglementCore = GetOrCreateEntanglementRelayCore();
            soil ??= GetOrCreateItem("Soil", "soil", "Soil", 1, 20);
            plasticSheet ??= GetOrCreateItem("PlasticSheet", "plastic_sheet", "Plastic Sheet", 1, 20);
            carbonDioxideCanister ??= GetOrCreateItem(
                "CarbonDioxideCanister", "carbon_dioxide_canister", "CO₂ Canister", 2, 10);

            BuildableDefinition wall = GetOrCreateBuildable("StoneWallBuildable", "stone_wall", "Stone Wall",
                Vector2Int.one, StoneWallSeconds, 1.5f, new Color(.58f, .5f, .44f),
                "A stacked regolith block. Cheap cover against the wind.",
                new CraftingItemAmount(stone, 4));
            BuildableDefinition barricade = GetOrCreateBuildable("MetalBarricadeBuildable", "metal_barricade",
                "Metal Barricade", new Vector2Int(2, 1), MetalBarricadeSeconds, 1.1f, new Color(.62f, .66f, .72f),
                "A braced alloy hull panel, two cells wide.",
                new CraftingItemAmount(aluminumAlloy, 3));
            BuildableDefinition fieldOven = GetOrCreateBuildable("FieldOvenBuildable", "field_oven", "Field Oven",
                new Vector2Int(2, 2), FieldOvenSeconds, 1.6f, new Color(.72f, .44f, .26f),
                "An outdoor range. Cooks the same dishes as the galley oven.",
                new CraftingItemAmount(aluminumAlloy, 4), new CraftingItemAmount(stone, 6));
            fieldOven.ConfigureCookingStation(oven);
            EditorUtility.SetDirty(fieldOven);
            BuildableDefinition placedOxygenCandle = GetOrCreateBuildable(
                "OxygenCandleBuildable", "oxygen_candle", "Oxygen Candle",
                Vector2Int.one, OxygenCandlePlacementSeconds, .8f, new Color(.72f, .78f, .82f),
                "A single-use chemical oxygen candle. It ignites after placement and feeds the pod reserve.",
                new CraftingItemAmount(oxygenCandle, 1));
            placedOxygenCandle.ConfigureOxygenCandle(true);
            EditorUtility.SetDirty(placedOxygenCandle);
            BuildableDefinition ironMiningDrill = GetOrCreateIronMiningDrill(aluminumAlloy: GetOrCreateAluminumAlloy());
            BuildableDefinition gravelExtractor = GetOrCreateGravelExtractor(GetOrCreateAluminumAlloy());
            PlanterBoxDefinition planterDefinition = AssetDatabase.LoadAssetAtPath<PlanterBoxDefinition>(PlanterBoxDefinitionPath);
            if (planterDefinition == null)
            {
                planterDefinition = ScriptableObject.CreateInstance<PlanterBoxDefinition>();
                planterDefinition.name = "Planter Box";
                AssetDatabase.CreateAsset(planterDefinition, PlanterBoxDefinitionPath);
            }
            planterDefinition.Configure(10000, 1000, 500f, 25f, 500f, 1f, 2f, 1f,
                carbonDioxideCanister, 50f);
            EditorUtility.SetDirty(planterDefinition);
            BuildableDefinition planterBox = GetOrCreateBuildable(
                "PlanterBoxBuildable", "planter_box", "Planter Box", Vector2Int.one,
                PlanterBoxBuildSeconds, 1f, new Color(.36f, .55f, .28f),
                "A one-cell growing system. Water and CO₂ inputs drive an oxygen output buffer.",
                new CraftingItemAmount(soil, 4), new CraftingItemAmount(plasticSheet, 2));
            Sprite planterSprite = WorldArtSetup.ImportBuildingSprite("PlanterBox");
            planterBox.ConfigurePresentation(planterSprite, 1f, new Color(.36f, .55f, .28f));
            planterBox.ConfigureIcon(planterSprite);
            planterBox.ConfigureTaggedCost(new TaggedBuildingMaterialAmount(ItemMaterialTag.MetalPlate, 2));
            planterBox.ConfigurePlanterBox(planterDefinition);
            EditorUtility.SetDirty(planterBox);

            BuildableDefinition transferPost = GetOrCreateBuildable(
                "ItemTransferPostBuildable", "item_transfer_post", "Transfer Post", Vector2Int.one,
                ItemTransferPostBuildSeconds, .65f, new Color(.22f, .28f, .34f),
                "A half-cell logistics endpoint. Right-click it to choose one adjacent or remote input and output.",
                new CraftingItemAmount(aluminumAlloy, 2),
                new CraftingItemAmount(plasticSheet, 1),
                new CraftingItemAmount(entanglementCore, 1));
            Sprite transferPostSprite = WorldArtSetup.ImportBuildingSprite("ItemTransferPost");
            Sprite transferPostColorMask = WorldArtSetup.ImportBuildingSprite("ItemTransferPostColorMask");
            transferPost.ConfigurePresentation(transferPostSprite, .65f, new Color(.22f, .28f, .34f));
            transferPost.ConfigureIcon(transferPostSprite);
            transferPost.ConfigureItemTransferPost(true);
            transferPost.ConfigureTransferColorMask(transferPostColorMask);
            EditorUtility.SetDirty(transferPost);

            BuildableDefinition solarPanel = GetOrCreateBuildable(
                "SolarPanelBuildable", "solar_panel", "Solar Panel", Vector2Int.one,
                SolarPanelBuildSeconds, .15f, new Color(.12f, .2f, .34f),
                "Generates 2 electricity units per second for adjacent mining drills. Place it beside a drill.",
                new CraftingItemAmount(aluminumAlloy, 3), new CraftingItemAmount(plasticSheet, 2));
            Sprite solarPanelSprite = WorldArtSetup.ImportBuildingSprite("SolarPanel");
            solarPanel.ConfigurePresentation(solarPanelSprite, .15f, new Color(.12f, .2f, .34f));
            solarPanel.ConfigureIcon(solarPanelSprite);
            solarPanel.ConfigureSolarPanel(true, SolarPanelElectricityPerSecond);
            EditorUtility.SetDirty(solarPanel);

            BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(BuildingCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
                catalog.name = "Default Building Catalog";
                AssetDatabase.CreateAsset(catalog, BuildingCatalogPath);
            }

            catalog.Configure(wall, barricade, fieldOven, placedOxygenCandle, ironMiningDrill, gravelExtractor,
                planterBox, transferPost, solarPanel);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static BuildableDefinition GetOrCreateIronMiningDrill(ItemDefinition aluminumAlloy)
        {
            ItemDefinition ironOre = AssetDatabase.LoadAssetAtPath<ItemDefinition>(IronOrePath)
                                     ?? GetOrCreateItem("IronOre", "iron_ore", "Iron Ore", 2, 20);
            ItemDefinition petroleum = GetOrCreatePetroleumCanister();
            MiningDrillDefinition definition = AssetDatabase.LoadAssetAtPath<MiningDrillDefinition>(MiningDrillPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MiningDrillDefinition>();
                definition.name = "Iron Mining Drill";
                AssetDatabase.CreateAsset(definition, MiningDrillPath);
            }

            definition.Configure(
                "iron", "iron", ironOre,
                IronMiningDrillProductionPerSecond, IronMiningDrillOreCapacity, IronMiningDrillOutputPerSecond,
                IronMiningDrillElectricityPerOre, IronMiningDrillElectricityCapacity,
                petroleum, PetroleumPerCanister, IronMiningDrillPetroleumPerOre,
                IronMiningDrillPetroleumCapacity);
            EditorUtility.SetDirty(definition);

            Sprite sprite = WorldArtSetup.ImportBuildingSprite("IronMiningDrill");
            BuildableDefinition buildable = GetOrCreateBuildable(
                "IronMiningDrillBuildable", "iron_mining_drill", "Iron Mining Drill",
                Vector2Int.one, IronMiningDrillBuildSeconds, .98f, new Color(.82f, .58f, .18f),
                "Extracts iron ore only when placed on iron terrain. Accepts electricity or petroleum and pauses when its ore bin is full.",
                new CraftingItemAmount(aluminumAlloy, 8));
            buildable.ConfigurePresentation(sprite, .98f, new Color(.82f, .58f, .18f));
            buildable.ConfigureIcon(sprite);
            buildable.ConfigureMiningDrill(definition);
            EditorUtility.SetDirty(buildable);
            return buildable;
        }

        private static BuildableDefinition GetOrCreateGravelExtractor(ItemDefinition aluminumAlloy)
        {
            ItemDefinition gravel = GetOrCreateGravel();
            ItemDefinition petroleum = GetOrCreatePetroleumCanister();
            MiningDrillDefinition definition =
                AssetDatabase.LoadAssetAtPath<MiningDrillDefinition>(GravelExtractorPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MiningDrillDefinition>();
                definition.name = "Gravel Extractor";
                AssetDatabase.CreateAsset(definition, GravelExtractorPath);
            }

            definition.name = "Gravel Extractor";
            definition.Configure(
                "regolith", "ordinary regolith", gravel,
                GravelExtractorProductionPerSecond, GravelExtractorCapacity, GravelExtractorOutputPerSecond,
                GravelExtractorElectricityPerItem, GravelExtractorElectricityCapacity,
                petroleum, PetroleumPerCanister, GravelExtractorPetroleumPerItem,
                GravelExtractorPetroleumCapacity);
            EditorUtility.SetDirty(definition);

            Sprite sprite = WorldArtSetup.ImportBuildingSprite("GravelExtractor");
            BuildableDefinition buildable = GetOrCreateBuildable(
                "GravelExtractorBuildable", "gravel_extractor", "Gravel Extractor",
                Vector2Int.one, GravelExtractorBuildSeconds, .98f, new Color(.72f, .56f, .22f),
                "Extracts gravel from ordinary regolith. Accepts electricity or petroleum and pauses when its output bin is full.",
                new CraftingItemAmount(aluminumAlloy, 6));
            buildable.ConfigurePresentation(sprite, .98f, new Color(.72f, .56f, .22f));
            buildable.ConfigureIcon(sprite);
            buildable.ConfigureMiningDrill(definition);
            EditorUtility.SetDirty(buildable);
            return buildable;
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
            string displayName, float duration, string requiredToolItemId,
            Vector3 scale, params ResourceYield[] yields)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            ResourceNodeDefinition node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
            if (node == null)
            {
                node = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
                node.name = displayName;
                AssetDatabase.CreateAsset(node, path);
            }

            node.Configure(resourceId, displayName, duration, 2.25f, requiredToolItemId, scale, yields);
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
                settings.Configure(DefaultStartingAreaSize, 8128);
                EditorUtility.SetDirty(settings);
                return settings;
            }

            settings = ScriptableObject.CreateInstance<TerrainGenerationSettings>();
            settings.name = "Default Terrain Settings";
            settings.Configure(DefaultStartingAreaSize, 8128);
            AssetDatabase.CreateAsset(settings, TerrainSettingsPath);
            return settings;
        }

        private static void CreateBootstrapScene(ItemDefinition energyBar, ItemDefinition potato,
            ItemDefinition aluminumAlloy, ItemDefinition chlorateSalt, ItemDefinition pickaxe,
            ItemDefinition petroleum, ItemDefinition shovel, ItemDefinition soil,
            ItemDefinition plasticSheet, ItemDefinition carbonDioxideCanister,
            ItemDefinition entanglementRelayCore, ItemDefinition carbonDioxideFilterCartridge)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Application");
            root.AddComponent<GameFlowController>().ConfigureStartingSupplies(
                energyBar, potato, aluminumAlloy, chlorateSalt, pickaxe, petroleum, shovel,
                soil, plasticSheet, carbonDioxideCanister, entanglementRelayCore, carbonDioxideFilterCartridge);
            root.AddComponent<BootstrapSceneEntry>();
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void UpdateBootstrapStartingSupplies(ItemDefinition energyBar, ItemDefinition potato,
            ItemDefinition aluminumAlloy, ItemDefinition chlorateSalt, ItemDefinition pickaxe,
            ItemDefinition petroleum)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            GameFlowController flowController = Object.FindFirstObjectByType<GameFlowController>();
            if (flowController == null)
            {
                Debug.LogError("The bootstrap scene has no game flow controller to receive starting supplies.");
                return;
            }

            flowController.ConfigureStartingSupplies(
                energyBar, potato, aluminumAlloy, chlorateSalt, pickaxe, petroleum,
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShovelPath),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(SoilPath),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(PlasticSheetPath),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(CarbonDioxideCanisterPath),
                GetOrCreateEntanglementRelayCore(),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>(CarbonDioxideFilterCartridgePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Main Menu").AddComponent<MainMenuView>();
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateGameplayScene(TerrainGenerationSettings settings,
            PlanetEnvironmentSettings environmentSettings, ResourceSpawnSettings resourceSpawnSettings,
            TerrainPatchSettings terrainPatchSettings, InventorySkin inventorySkin,
            WorldVisualSettings worldVisuals, BuildingCatalog buildingCatalog, CraftingCatalog craftingCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameplay = new GameObject("Gameplay");
            GameBootstrap bootstrap = gameplay.AddComponent<GameBootstrap>();
            bootstrap.Configure(settings, environmentSettings, resourceSpawnSettings);
            bootstrap.ConfigureTerrainPatches(terrainPatchSettings);
            bootstrap.ConfigureUi(inventorySkin);
            bootstrap.ConfigureVisuals(worldVisuals);
            bootstrap.ConfigureBuilding(buildingCatalog);
            bootstrap.ConfigureCrafting(craftingCatalog);
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
