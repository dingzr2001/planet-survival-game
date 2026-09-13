using PlanetSurvival.Bootstrap;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
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

        // A 32m chunk carries 0.2875 nodes on average, about one node per 3560m². The surface is meant to
        // feel genuinely scarce, so expeditions cross several chunks between useful deposits.
        private const float ResourceChunkSize = 32f;
        private const int ResourceLoadRadiusInChunks = 2;
        private const float ResourceMinimumSpacing = 8f;
        private const float RockNodesPerChunk = .125f;
        private const float DebrisNodesPerChunk = .0625f;

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
        private const string PickaxePath = ConfigurationDirectory + "/Pickaxe.asset";
        private const string PickaxeVisualPath = ConfigurationDirectory + "/PickaxeVisual.asset";
        private const string PickaxeSwingPath = ConfigurationDirectory + "/PickaxeSwing.asset";
        private const string PickaxeAnimationPath = ConfigurationDirectory + "/PickaxeAnimation.asset";
        private const string TerrainPatchSettingsPath = ConfigurationDirectory + "/DefaultTerrainPatches.asset";

        // Rock terrain. The three grades share the same texture resolution and the same 1 stone per swing,
        // so their hardness is the only thing that separates them: loose scree gives up at once, the
        // boulder field takes five swings and pays five times as much. Together they cover about a
        // quarter of the surface, which TerrainPatchLayerTests pins down.
        private const int TerrainSeedOffset = 5231;
        private const float TerrainTileSize = 3f;
        private const int TerrainChunkSizeInTiles = 8;
        private const int TerrainLoadRadiusInChunks = 1;
        private const float TerrainTextureTileSize = 8f;
        private const float RockDigSeconds = 1.6f;
        private const int RockStonePerDig = 1;

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
        private const float IceNodesPerChunk = .1f;

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
            ItemDefinition pickaxe = GetOrCreatePickaxe();
            PlayerToolAnimationDefinition pickaxeAnimation = GetOrCreatePickaxeAnimation(pickaxe);
            worldVisuals.ConfigurePlayerToolAnimations(pickaxeAnimation);
            EditorUtility.SetDirty(worldVisuals);
            ItemDefinition iceChunk = GetOrCreateIceChunk();
            CropDefinition potatoCrop = GetOrCreatePotatoCrop(potato);
            ResourceSpawnSettings resourceSpawnSettings = GetOrCreateResourceSettings(iceChunk, pickaxe);
            TerrainPatchSettings terrainPatchSettings = GetOrCreateTerrainPatchSettings(pickaxe);
            CookingStationDefinition oven = GetOrCreateOven(potato);
            BuildingCatalog buildingCatalog = GetOrCreateBuildingCatalog(oven);
            WorldArtSetup.AssignResourceSprites();
            WorldArtSetup.ConfigureInteriorPropSprites();
            UiArtSetup.AssignItemIcons();
            CreateBootstrapScene(energyBar, potato, aluminumAlloy, pickaxe);
            CreateMainMenuScene();
            CreateLandingPodScene(LandingPodDeck.Habitat, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodHabitatScenePath);
            CreateLandingPodScene(LandingPodDeck.Cargo, environmentSettings, inventorySkin, worldVisuals,
                oven, potatoCrop, iceChunk, LandingPodCargoScenePath);
            CreateGameplayScene(settings, environmentSettings, resourceSpawnSettings, terrainPatchSettings,
                inventorySkin, worldVisuals, buildingCatalog);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Planet Survival formal scenes and default configuration are ready.");
        }

        private static ResourceSpawnSettings GetOrCreateResourceSettings(
            ItemDefinition iceChunk, ItemDefinition pickaxe)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);
            ItemDefinition scrap = GetOrCreateItem("MetalScrap", "metal_scrap", "Metal Scrap", 2, 10);

            ResourceNodeDefinition rock = GetOrCreateNode("RockNode", "rock", "Rock", 2.5f,
                pickaxe.ItemId,
                new Vector3(.9f, 1.1f, .72f), new ResourceYield(stone, 2));
            ResourceNodeDefinition debris = GetOrCreateNode("DebrisNode", "debris", "Debris", 3.5f,
                string.Empty,
                new Vector3(1f, 1f, .8f), new ResourceYield(scrap, 1));
            // Physical sizes are world-space values rather than tile counts. Repeated entries weight the mix:
            // small remnants remain, but most deposits read as substantial connected sheets.
            ResourceNodeDefinition iceDeposit = GetOrCreateNode("IceDepositNode", "ice_deposit", "Ice Deposit",
                IceGatherSeconds, string.Empty, new Vector3(1.1f, .15f, 1.1f),
                new ResourceYield(iceChunk, IceChunksPerDeposit));
            // The sheet lies flat on the ground: the explorer walks over it rather than around it.
            iceDeposit.ConfigureCollision(false);
            iceDeposit.ConfigurePresentation(ResourceVisualMode.GroundDecal);
            iceDeposit.ConfigureBlobShadow(false);
            iceDeposit.ConfigureGroundPatchSizes(
                new Vector2(1.1f, 1.1f),
                new Vector2(2.25f, 2.1f), new Vector2(2.25f, 2.1f), new Vector2(2.25f, 2.1f),
                new Vector2(2.4f, 3.35f), new Vector2(3.45f, 2.3f),
                new Vector2(3.6f, 3.25f));
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
                new ResourceSpawnEntry(rock, RockNodesPerChunk),
                new ResourceSpawnEntry(debris, DebrisNodesPerChunk),
                new ResourceSpawnEntry(iceDeposit, IceNodesPerChunk));
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// Authors the patched terrain: three grades of rock laid over the base regolith. Hardest first,
        /// because the first layer to reach its threshold wins the tile, and a wide soft-scree patch would
        /// otherwise swallow the boulder fields sitting inside it. Each layer samples its own field, so
        /// the grades mix into one another rather than forming concentric rings.
        /// </summary>
        private static TerrainPatchSettings GetOrCreateTerrainPatchSettings(ItemDefinition pickaxe)
        {
            ItemDefinition stone = GetOrCreateItem("RawStone", "raw_stone", "Raw Stone", 1, 20);

            // The artwork is a cutout layer of loose rock over the regolith, and how much of the ground it
            // hides rises with its grade: scattered gravel, then broken slabs, then solid boulders. That
            // makes a tile's hardness readable before the first swing.
            TerrainSurfaceDefinition boulderField = GetOrCreateTerrainSurface("BoulderFieldTerrain",
                "rock_boulder_field", "Boulder Field", "Stone3", 5, pickaxe.ItemId, stone);
            TerrainSurfaceDefinition brokenRock = GetOrCreateTerrainSurface("BrokenRockTerrain",
                "rock_broken", "Broken Rock", "Stone2", 3, pickaxe.ItemId, stone);
            TerrainSurfaceDefinition looseScree = GetOrCreateTerrainSurface("LooseScreeTerrain",
                "rock_loose_scree", "Loose Scree", "Stone1", 1, pickaxe.ItemId, stone);

            TerrainPatchSettings settings =
                AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(TerrainPatchSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
                settings.name = "Default Terrain Patches";
                AssetDatabase.CreateAsset(settings, TerrainPatchSettingsPath);
            }

            // Patch sizes run from tight boulder fields to broad scree flats, and the thresholds are read
            // off the field's distribution rather than guessed: see TerrainPatchLayerTests, which fails if
            // a change to the noise moves the covered fraction out of its band.
            settings.Configure(TerrainSeedOffset, TerrainTileSize, TerrainChunkSizeInTiles,
                TerrainLoadRadiusInChunks,
                new TerrainPatchLayer(boulderField, 15f, .78f, 1613),
                new TerrainPatchLayer(brokenRock, 20f, .73f, 7817),
                new TerrainPatchLayer(looseScree, 28f, .68f, 3271));
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static TerrainSurfaceDefinition GetOrCreateTerrainSurface(string assetName, string terrainId,
            string displayName, string textureName, int digCount, string requiredToolItemId,
            ItemDefinition yieldItem)
        {
            string path = $"{ConfigurationDirectory}/{assetName}.asset";
            TerrainSurfaceDefinition surface = AssetDatabase.LoadAssetAtPath<TerrainSurfaceDefinition>(path);
            if (surface == null)
            {
                surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
                surface.name = displayName;
                AssetDatabase.CreateAsset(surface, path);
            }

            surface.Configure(terrainId, displayName, digCount, RockDigSeconds, requiredToolItemId,
                new ResourceYield(yieldItem, RockStonePerDig));
            surface.ConfigureTexture(WorldArtSetup.ImportGroundTexture(textureName), TerrainTextureTileSize);
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
            ItemDefinition aluminumAlloy, ItemDefinition pickaxe)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Application");
            root.AddComponent<GameFlowController>().ConfigureStartingSupplies(
                energyBar, potato, aluminumAlloy, pickaxe);
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
            TerrainPatchSettings terrainPatchSettings, InventorySkin inventorySkin,
            WorldVisualSettings worldVisuals, BuildingCatalog buildingCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameplay = new GameObject("Gameplay");
            GameBootstrap bootstrap = gameplay.AddComponent<GameBootstrap>();
            bootstrap.Configure(settings, environmentSettings, resourceSpawnSettings);
            bootstrap.ConfigureTerrainPatches(terrainPatchSettings);
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
