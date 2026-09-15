using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Animation;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Ground;
using PlanetSurvival.World.Presentation;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class FormalSceneConfigurationTests
    {
        private static readonly string[] ExpectedScenePaths =
        {
            "Assets/Game/Scenes/Bootstrap.unity",
            "Assets/Game/Scenes/MainMenu.unity",
            "Assets/Game/Scenes/LandingPodHabitat.unity",
            "Assets/Game/Scenes/LandingPodCargo.unity",
            "Assets/Game/Scenes/Gameplay.unity"
        };

        [Test]
        public void BuildSettings_ContainsFormalScenesInStartupOrder()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Has.Length.EqualTo(ExpectedScenePaths.Length));
            for (int i = 0; i < ExpectedScenePaths.Length; i++)
            {
                Assert.That(scenes[i].enabled, Is.True);
                Assert.That(scenes[i].path, Is.EqualTo(ExpectedScenePaths[i]));
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[i].path), Is.Not.Null);
            }
        }

        [Test]
        public void OxygenHudTexture_IsImportedForTransparentUiRendering()
        {
            const string path = "Assets/Game/Resources/Oxygen/Oxygen.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(texture, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        }

        [Test]
        public void MinimapFrame_IsAvailableAsTransparentUiTexture()
        {
            const string path = "Assets/Game/Resources/UI/MinimapFrame.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(texture, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
        }

        [Test]
        public void EnergyBar_HasNutritionAndTransparentInventoryIcon()
        {
            const string itemPath = "Assets/Game/Configuration/EnergyBar.asset";
            const string iconPath = "Assets/Game/Art/UI/Icons/Items/EnergyBar.png";
            ItemDefinition energyBar = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

            Assert.That(energyBar, Is.Not.Null);
            Assert.That(energyBar.ItemId, Is.EqualTo("energy_bar"));
            Assert.That(energyBar.Calories, Is.EqualTo(500));
            Assert.That(energyBar.CanUse, Is.True);
            Assert.That(energyBar.Icon, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        }

        [Test]
        public void Potato_IsRawVegetableIngredientWithTransparentInventoryIcon()
        {
            const string itemPath = "Assets/Game/Configuration/Potato.asset";
            const string iconPath = "Assets/Game/Art/UI/Icons/Items/Potato.png";
            ItemDefinition potato = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

            Assert.That(potato, Is.Not.Null);
            Assert.That(potato.ItemId, Is.EqualTo("potato"));
            Assert.That(potato.CanUse, Is.False);
            Assert.That(potato.Calories, Is.Zero);
            Assert.That(potato.Icon, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        }

        [Test]
        public void AluminumAlloy_IsBuildingMaterialWithTransparentInventoryIcon()
        {
            const string itemPath = "Assets/Game/Configuration/AluminumAlloy.asset";
            const string iconPath = "Assets/Game/Art/UI/Icons/Items/AluminumAlloy.png";
            ItemDefinition aluminumAlloy = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

            Assert.That(aluminumAlloy, Is.Not.Null);
            Assert.That(aluminumAlloy.ItemId, Is.EqualTo("aluminum_alloy"));
            Assert.That(aluminumAlloy.CanUse, Is.False);
            Assert.That(aluminumAlloy.Calories, Is.Zero);
            Assert.That(aluminumAlloy.MaximumStackSize,
                Is.GreaterThanOrEqualTo(GameSessionState.InitialAluminumAlloyCount),
                "The starting stock must fit in a single cargo stack.");
            Assert.That(aluminumAlloy.Icon, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        }

        [Test]
        public void RoastPotato_IsCookedFoodWithTransparentInventoryIcon()
        {
            const string itemPath = "Assets/Game/Configuration/RoastPotato.asset";
            const string iconPath = "Assets/Game/Art/UI/Icons/Items/RoastPotato.png";
            ItemDefinition roastPotato = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

            Assert.That(roastPotato, Is.Not.Null);
            Assert.That(roastPotato.ItemId, Is.EqualTo("roast_potato"));
            Assert.That(roastPotato.CanUse, Is.True);
            Assert.That(roastPotato.Calories, Is.GreaterThan(0));
            Assert.That(roastPotato.Icon, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        }

        /// <summary>
        /// The surface half of the ice-water-food loop: ice has to be gatherable, and it has to be worth
        /// nothing until the processor has purified it.
        /// </summary>
        [Test]
        public void IceChunk_IsGatheredOnTheSurfaceAndCarriesNoNutrition()
        {
            ItemDefinition ice = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/Game/Configuration/IceChunk.asset");
            ResourceNodeDefinition deposit = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(
                "Assets/Game/Configuration/IceDepositNode.asset");
            ResourceSpawnSettings spawnSettings = AssetDatabase.LoadAssetAtPath<ResourceSpawnSettings>(
                "Assets/Game/Configuration/DefaultResourceSpawnSettings.asset");

            Assert.That(ice, Is.Not.Null);
            Assert.That(ice.ItemId, Is.EqualTo("ice_chunk"));
            Assert.That(ice.Calories, Is.Zero);
            Assert.That(ice.CanUse, Is.False, "Ice is only worth anything once the processor has purified it.");

            Assert.That(deposit, Is.Not.Null);
            Assert.That(deposit.IsValid(out string depositError), Is.True, depositError);
            Assert.That(deposit.WorldSprite, Is.Not.Null,
                "Without its cutout the deposit is drawn as a placeholder block on the surface.");
            Assert.That(deposit.Yields.Count, Is.EqualTo(1));
            Assert.That(deposit.Yields[0].Item.ItemId, Is.EqualTo("ice_chunk"));
            Assert.That(deposit.RequiredToolItemId, Is.Empty,
                "Water must stay reachable with bare hands until tools exist.");
            Assert.That(deposit.BlocksMovement, Is.False,
                "The sheet lies flat on the ground; the explorer walks over it rather than around it.");
            Assert.That(deposit.VisualMode, Is.EqualTo(ResourceVisualMode.GroundDecal),
                "Ice is a floor surface and must stay below actors instead of joining billboard depth sorting.");
            Assert.That(deposit.CastsBlobShadow, Is.False,
                "A ground-hugging ice slab must not receive a floating-object shadow.");

            Assert.That(spawnSettings, Is.Not.Null);
            bool spawnsIce = false;
            for (int i = 0; i < spawnSettings.Entries.Count; i++)
            {
                ResourceSpawnEntry entry = spawnSettings.Entries[i];
                if (entry.Definition == deposit)
                {
                    spawnsIce = entry.NodesPerChunk > 0f;
                }
            }

            Assert.That(spawnsIce, Is.True, "Ice deposits must be part of the streamed surface layout.");
        }

        /// <summary>
        /// Ordinary rock makes the surface worth walking across rather than through: three grades break
        /// down in one, three and five swings, each paying out on every swing.
        /// </summary>
        [Test]
        public void RockTerrain_CoversTheRegolithInThreeDiggableGrades()
        {
            TerrainPatchSettings patches = AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(
                "Assets/Game/Configuration/DefaultTerrainPatches.asset");

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Layers.Count, Is.EqualTo(4));

            var digCounts = new List<int>();
            for (int i = 0; i < patches.Layers.Count; i++)
            {
                TerrainSurfaceDefinition surface = patches.Layers[i].Surface;
                if (surface.TerrainId == "iron")
                {
                    continue;
                }

                Assert.That(surface, Is.Not.Null);
                Assert.That(surface.IsValid(out string error), Is.True, error);
                Assert.That(surface.Texture, Is.Not.Null,
                    $"Terrain '{surface.TerrainId}' without its texture draws as untextured white ground.");
                Assert.That(surface.RequiredToolItemId, Is.EqualTo("pickaxe"),
                    "Rock is what the pickaxe is for.");
                Assert.That(surface.Yields.Count, Is.EqualTo(1));
                Assert.That(surface.Yields[0].Item.ItemId, Is.EqualTo("raw_stone"));
                digCounts.Add(surface.DigCount);

                Assert.That(patches.Layers[i].TargetCoverage, Is.GreaterThan(0f),
                    "A layer with zero target coverage never generates.");
            }

            Assert.That(digCounts, Is.EquivalentTo(new[] { 1, 3, 5 }));
            // Hardest first: the first matching layer wins the tile, so a soft patch listed
            // ahead of a hard one would swallow the hard cores sitting inside it.
            Assert.That(digCounts[0], Is.GreaterThan(digCounts[^1]));
        }

        /// <summary>
        /// Rarity alone does not make a deposit worth walking to — patch width decides whether one percent of
        /// the ground arrives as deposits or as specks. At the rock grades' width iron came out about four
        /// tiles, ninety seconds of mining at the end of a long walk; this pins the deposits at a size
        /// that survives several backpack loads.
        /// </summary>
        [Test]
        public void IronDeposits_AreWholeDepositsRatherThanSpecks()
        {
            TerrainGenerationSettings terrain = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(
                "Assets/Game/Configuration/DefaultTerrainSettings.asset");
            TerrainPatchSettings patches = AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(
                "Assets/Game/Configuration/DefaultTerrainPatches.asset");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(patches, Is.Not.Null);

            var map = new TerrainTileMap();
            map.Configure(terrain.Seed + patches.SeedOffset, patches);

            const int side = 300;
            var isIron = new bool[side, side];
            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    var tile = new TerrainTileCoordinate(x - side / 2, z - side / 2);
                    TerrainSurfaceDefinition surface = map.GetSurface(tile);
                    isIron[x, z] = surface != null && surface.TerrainId == "iron";
                }
            }

            List<int> deposits = MeasureDepositSizes(isIron, side);
            Assert.That(deposits, Is.Not.Empty, "No iron at all within 900 m of the landing site.");
            deposits.Sort();
            int median = deposits[deposits.Count / 2];
            int specks = 0;
            for (int i = 0; i < deposits.Count; i++)
            {
                if (deposits[i] <= 3)
                {
                    specks++;
                }
            }

            Assert.That(median, Is.GreaterThanOrEqualTo(8),
                $"The median iron deposit is {median} tiles; that is a detour, not a mining stop.");
            Assert.That(specks / (float)deposits.Count, Is.LessThan(.2f),
                "Too many iron deposits are a tile or three — a long walk should not end in a speck.");
        }

        /// <summary>Sizes of the connected runs of marked tiles, in tiles.</summary>
        private static List<int> MeasureDepositSizes(bool[,] marked, int side)
        {
            var visited = new bool[side, side];
            var sizes = new List<int>();
            var frontier = new Stack<Vector2Int>();

            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    if (!marked[x, z] || visited[x, z])
                    {
                        continue;
                    }

                    int size = 0;
                    frontier.Push(new Vector2Int(x, z));
                    visited[x, z] = true;
                    while (frontier.Count > 0)
                    {
                        Vector2Int cell = frontier.Pop();
                        size++;
                        foreach (Vector2Int step in NeighbourSteps)
                        {
                            int nextX = cell.x + step.x;
                            int nextZ = cell.y + step.y;
                            if (nextX < 0 || nextZ < 0 || nextX >= side || nextZ >= side
                                || visited[nextX, nextZ] || !marked[nextX, nextZ])
                            {
                                continue;
                            }

                            visited[nextX, nextZ] = true;
                            frontier.Push(new Vector2Int(nextX, nextZ));
                        }
                    }

                    sizes.Add(size);
                }
            }

            return sizes;
        }

        private static readonly Vector2Int[] NeighbourSteps =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        [Test]
        public void IronTerrain_IsRareSlowAndRequiresThePickaxe()
        {
            TerrainPatchSettings patches = AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(
                "Assets/Game/Configuration/DefaultTerrainPatches.asset");
            ItemDefinition ironOre = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/Game/Configuration/IronOre.asset");

            Assert.That(patches, Is.Not.Null);
            Assert.That(ironOre, Is.Not.Null);
            Assert.That(ironOre.ItemId, Is.EqualTo("iron_ore"));
            Assert.That(ironOre.Icon, Is.Not.Null);

            TerrainPatchLayer ironLayer = patches.Layers[0];
            TerrainSurfaceDefinition iron = ironLayer.Surface;
            Assert.That(iron, Is.Not.Null);
            Assert.That(iron.TerrainId, Is.EqualTo("iron"),
                "The rare deposit must win overlaps with ordinary rock.");
            Assert.That(iron.IsValid(out string error), Is.True, error);
            Assert.That(iron.Texture, Is.Not.Null);
            Assert.That(iron.RequiredToolItemId, Is.EqualTo("pickaxe"));
            Assert.That(iron.DigCount, Is.GreaterThan(5));
            Assert.That(iron.DigDuration, Is.GreaterThan(1.6f));
            Assert.That(iron.Yields.Count, Is.EqualTo(1));
            Assert.That(iron.Yields[0].Item, Is.EqualTo(ironOre));
            Assert.That(ironLayer.TargetCoverage, Is.EqualTo(.01f).Within(.0001f));
            Assert.That(ironLayer.TargetCoverage, Is.LessThan(patches.Layers[1].TargetCoverage));
        }

        /// <summary>
        /// The share of the surface each grade takes, measured on the world the game actually builds —
        /// the shipped assets, the seed <c>GameBootstrap</c> derives, and the priority rule. The
        /// shares are a design decision (see ProjectSceneSetup): rock punctuates the regolith rather than
        /// replacing it, and each grade is half as common as the one below, so all three contribute about
        /// the same stone per square metre.
        /// </summary>
        [Test]
        public void RockTerrain_CoversTheDesignedShareOfTheSurface()
        {
            TerrainGenerationSettings terrain = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(
                "Assets/Game/Configuration/DefaultTerrainSettings.asset");
            TerrainPatchSettings patches = AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(
                "Assets/Game/Configuration/DefaultTerrainPatches.asset");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(patches, Is.Not.Null);

            var map = new TerrainTileMap();
            map.Configure(terrain.Seed + patches.SeedOffset, patches);

            const int side = 300;
            var counts = new int[patches.Layers.Count];
            var covered = new bool[side, side];
            var ironCovered = new bool[side, side];
            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    var tile = new TerrainTileCoordinate(x - side / 2, z - side / 2);
                    int layerIndex = map.GetLayerIndex(tile);
                    if (layerIndex != ClusteredTerrainLayout.BaseLayerIndex)
                    {
                        counts[layerIndex]++;
                        string terrainId = patches.Layers[layerIndex].Surface.TerrainId;
                        covered[x, z] = terrainId.StartsWith("rock_");
                        ironCovered[x, z] = terrainId == "iron";
                    }
                }
            }

            int total = side * side;
            var expectedShares = new Dictionary<string, float>
            {
                { "iron", .01f },
                { "rock_boulder_field", .02f },
                { "rock_broken", .04f },
                { "rock_loose_scree", .08f }
            };

            float rockShare = 0f;
            for (int i = 0; i < patches.Layers.Count; i++)
            {
                string terrainId = patches.Layers[i].Surface.TerrainId;
                Assert.That(expectedShares.ContainsKey(terrainId), Is.True,
                    $"Terrain '{terrainId}' has no designed share; give it one rather than letting it "
                    + "take an accidental amount of the world.");
                float share = counts[i] / (float)total;
                float expected = expectedShares[terrainId];
                if (terrainId.StartsWith("rock_"))
                {
                    rockShare += share;
                }
                Assert.That(patches.Layers[i].TargetCoverage, Is.EqualTo(expected).Within(.0001f),
                    $"'{terrainId}' should expose its intended rarity directly in the terrain asset.");
                // A quarter either way: tight enough to catch a noise distribution that drifted off design,
                // loose enough that the measured window is not doing the deciding.
                Assert.That(share, Is.InRange(expected * .75f, expected * 1.25f),
                    $"'{terrainId}' covers {share:P1}; it was designed for {expected:P0}.");
            }

            Assert.That(rockShare, Is.InRange(.11f, .17f), "Rock should punctuate the regolith, not carpet it.");
            Assert.That(IsolatedRockFraction(covered, side), Is.LessThan(.05f),
                "Rock is meant to generate in runs; almost every tile should touch another rock tile.");
            Assert.That(HasAdjacentCoveredTiles(ironCovered, side), Is.True,
                "Rare iron still has to support connected multi-tile veins, not only isolated specks.");
        }

        private static bool HasAdjacentCoveredTiles(bool[,] covered, int side)
        {
            for (int x = 1; x < side; x++)
            {
                for (int z = 1; z < side; z++)
                {
                    if (covered[x, z] && (covered[x - 1, z] || covered[x, z - 1]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float IsolatedRockFraction(bool[,] covered, int side)
        {
            int rock = 0;
            int isolated = 0;
            for (int x = 1; x < side - 1; x++)
            {
                for (int z = 1; z < side - 1; z++)
                {
                    if (!covered[x, z])
                    {
                        continue;
                    }

                    rock++;
                    if (!covered[x - 1, z] && !covered[x + 1, z] && !covered[x, z - 1] && !covered[x, z + 1])
                    {
                        isolated++;
                    }
                }
            }

            return rock == 0 ? 1f : isolated / (float)rock;
        }

        /// <summary>
        /// Rock artwork is a cutout layer that repeats across tiles rather than being stretched over one,
        /// so it has to be imported as tiling ground with its transparency intact.
        /// </summary>
        [Test]
        public void RockTerrainTextures_AreImportedAsTilingTransparentGround()
        {
            foreach (string textureName in new[] { "Stone1", "Stone2", "Stone3", "Iron" })
            {
                string path = $"Assets/Game/Art/World/Ground/{textureName}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                Assert.That(importer, Is.Not.Null, $"'{path}' was not imported.");
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat),
                    $"'{textureName}' would show a hard seam at every tile border without repeat wrapping.");
                Assert.That(importer.mipmapEnabled, Is.True,
                    $"'{textureName}' would shimmer at distance without mipmaps.");
                Assert.That(importer.alphaIsTransparency, Is.True,
                    $"'{textureName}' is a cutout; without this its trimmed edges pick up dark halos.");
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True,
                    $"'{textureName}' must keep an alpha channel, or it paints over the regolith it lies on.");
            }
        }

        /// <summary>
        /// The overlay shares the transparent queue with everything else lying on the floor, so its order
        /// is explicit rather than left to chance.
        /// </summary>
        [Test]
        public void RockTerrainOverlay_DrawsUnderTheThingsRestingOnIt()
        {
            Assert.That(TerrainChunkView.SortingOrder, Is.LessThan(GroundDecalView.SortingOrder),
                "An ice sheet lying on rock has to draw on top of the rock, not under it.");
            Assert.That(TerrainChunkView.SurfaceHeight, Is.GreaterThan(0f),
                "The overlay has to clear the base ground disc for the depth test.");
        }

        /// <summary>
        /// The habitat half of the loop. A harvest that only returned its own seed would leave the
        /// expedition exactly as doomed as it was before hydroponics existed.
        /// </summary>
        [Test]
        public void PotatoCrop_TurnsWaterAndOneSeedIntoMoreFood()
        {
            CropDefinition crop = AssetDatabase.LoadAssetAtPath<CropDefinition>(
                "Assets/Game/Configuration/PotatoCrop.asset");

            Assert.That(crop, Is.Not.Null);
            Assert.That(crop.IsValid(out string cropError), Is.True, cropError);
            Assert.That(crop.CropId, Is.EqualTo("potato_crop"));
            Assert.That(crop.SeedItem.ItemId, Is.EqualTo("potato"));
            Assert.That(crop.HarvestItem.ItemId, Is.EqualTo("potato"));
            Assert.That(crop.HarvestQuantity, Is.GreaterThan(crop.SeedQuantity));
            Assert.That(crop.GrowthGameHours, Is.GreaterThan(0f));
            Assert.That(crop.WaterMilliliters, Is.GreaterThan(0),
                "Growing food has to compete with drinking for the same reserve.");
        }

        [Test]
        public void Oven_ServesTheRoastPotatoRecipe()
        {
            CookingStationDefinition oven = AssetDatabase.LoadAssetAtPath<CookingStationDefinition>(
                "Assets/Game/Configuration/OvenStation.asset");
            CraftingRecipe recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(
                "Assets/Game/Configuration/RoastPotatoRecipe.asset");

            Assert.That(oven, Is.Not.Null);
            Assert.That(recipe, Is.Not.Null);
            Assert.That(oven.IsValid(out string stationError), Is.True, stationError);
            Assert.That(oven.StationId, Is.EqualTo("oven"));
            Assert.That(oven.Recipes, Does.Contain(recipe));
            Assert.That(oven.Supports(recipe), Is.True);

            Assert.That(recipe.IsValid(out string recipeError), Is.True, recipeError);
            Assert.That(recipe.DurationSeconds, Is.GreaterThan(0f));
            Assert.That(recipe.Inputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Inputs[0].Item.ItemId, Is.EqualTo("potato"));
            Assert.That(recipe.Outputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Outputs[0].Item.ItemId, Is.EqualTo("roast_potato"));
        }

        [Test]
        public void DefaultTerrainSettings_HasValidContinuousPlayableDimensions()
        {
            TerrainGenerationSettings settings = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(
                "Assets/Game/Configuration/DefaultTerrainSettings.asset");

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.StartingAreaSize.x, Is.EqualTo(48f));
            Assert.That(settings.StartingAreaSize.y, Is.EqualTo(48f));
            Assert.That(settings.StartingAreaCenter, Is.EqualTo(new Vector3(24f, 0f, 24f)));
            Assert.That(settings.Seed, Is.EqualTo(8128));
        }

        [Test]
        public void WorldVisualConfiguration_UsesFrameAnimationForCabinExplorerAndSurfaceRobot()
        {
            WorldVisualSettings visuals = AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(
                "Assets/Game/Configuration/DefaultWorldVisuals.asset");
            ResourceNodeDefinition rock = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(
                "Assets/Game/Configuration/RockNode.asset");

            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.GroundTexture, Is.Not.Null);
            Assert.That(visuals.PlayerSprite, Is.Not.Null);
            Assert.That(visuals.PlayerAnimationSheet, Is.Not.Null);
            Assert.That(visuals.PlayerAnimationSheet.width % 8, Is.Zero);
            Assert.That(visuals.PlayerAnimationSheet.height % 4, Is.Zero);
            Assert.That(visuals.PlayerFramesPerDirection, Is.EqualTo(8));
            Assert.That(visuals.PlayerFrameRects.Count, Is.EqualTo(32));
            Assert.That(visuals.PlayerFramePivots.Count, Is.EqualTo(32));
            Assert.That(visuals.ModularPlayerRig, Is.Null,
                "The experimental cutout rig must not replace either full-frame character.");
            Assert.That(visuals.SurfaceRobotAnimationSheet, Is.Not.Null);
            Assert.That(visuals.SurfaceRobotFramesPerDirection, Is.EqualTo(4));
            Assert.That(visuals.SurfaceRobotFrameRects.Count, Is.EqualTo(16));
            Assert.That(visuals.SurfaceRobotFramePivots.Count, Is.EqualTo(16));
            Assert.That(visuals.SurfaceRobotHeight, Is.GreaterThan(0f));
            Assert.That(visuals.HasSurfaceRobotAnimation, Is.True);
            Assert.That(visuals.LandingPodExteriorSprite, Is.Not.Null);
            Assert.That(visuals.LandingPodExteriorHeight, Is.GreaterThan(0f));
            for (int row = 0; row < 4; row++)
            {
                Vector2 directionPivot = visuals.PlayerFramePivots[row * 8];
                for (int column = 0; column < 8; column++)
                {
                    int index = row * 8 + column;
                    int left = visuals.PlayerAnimationSheet.width * column / 8;
                    int right = visuals.PlayerAnimationSheet.width * (column + 1) / 8;
                    int bottom = visuals.PlayerAnimationSheet.height * (3 - row) / 4;
                    int top = visuals.PlayerAnimationSheet.height * (4 - row) / 4;
                    Assert.That(visuals.PlayerFrameRects[index], Is.EqualTo(new Rect(
                        left, bottom, right - left, top - bottom)));
                    Assert.That(visuals.PlayerFramePivots[index], Is.EqualTo(directionPivot),
                        $"Direction row {row} must use one stable pivot across all frames.");
                }
            }

            for (int row = 0; row < 4; row++)
            {
                Vector2 directionPivot = visuals.SurfaceRobotFramePivots[row * 4];
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column;
                    int left = visuals.SurfaceRobotAnimationSheet.width * column / 4;
                    int right = visuals.SurfaceRobotAnimationSheet.width * (column + 1) / 4;
                    int bottom = visuals.SurfaceRobotAnimationSheet.height * (3 - row) / 4;
                    int top = visuals.SurfaceRobotAnimationSheet.height * (4 - row) / 4;
                    Assert.That(visuals.SurfaceRobotFrameRects[index], Is.EqualTo(new Rect(
                        left, bottom, right - left, top - bottom)));
                    Assert.That(visuals.SurfaceRobotFramePivots[index], Is.EqualTo(directionPivot),
                        $"Robot direction row {row} must use one stable ground anchor.");
                }
            }

            var robotImporter = AssetImporter.GetAtPath(
                "Assets/Game/Art/World/Characters/TrackedRobotDirectional4.png") as TextureImporter;
            Assert.That(robotImporter, Is.Not.Null);
            Assert.That(robotImporter.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(robotImporter.alphaIsTransparency, Is.True);
            Assert.That(robotImporter.mipmapEnabled, Is.False);
            Assert.That(rock, Is.Not.Null);
            Assert.That(rock.WorldSprite, Is.Not.Null);
        }

        [Test]
        public void Pickaxe_UsesIndependentToolArtAndIsRequiredForRock()
        {
            ItemDefinition pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/Game/Configuration/Pickaxe.asset");
            ResourceNodeDefinition rock = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(
                "Assets/Game/Configuration/RockNode.asset");
            WorldVisualSettings visuals = AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(
                "Assets/Game/Configuration/DefaultWorldVisuals.asset");
            PlayerToolAnimationDefinition animation =
                AssetDatabase.LoadAssetAtPath<PlayerToolAnimationDefinition>(
                    "Assets/Game/Configuration/PickaxeAnimation.asset");
            var importer = AssetImporter.GetAtPath(
                "Assets/Game/Art/World/Equipment/Pickaxe.png") as TextureImporter;

            Assert.That(pickaxe, Is.Not.Null);
            Assert.That(pickaxe.ItemId, Is.EqualTo("pickaxe"));
            Assert.That(pickaxe.Icon, Is.Not.Null);
            Assert.That(pickaxe.MaximumStackSize, Is.EqualTo(1));
            Assert.That(rock.RequiredToolItemId, Is.EqualTo(pickaxe.ItemId));
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.IsValid(out string error), Is.True, error);
            Assert.That(visuals.PlayerToolAnimations, Does.Contain(animation));
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
        }
    }
}
