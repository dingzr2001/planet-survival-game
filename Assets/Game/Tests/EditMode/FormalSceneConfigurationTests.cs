using NUnit.Framework;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.World.Generation;
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
        public void DefaultTerrainSettings_HasValidPlayableDimensions()
        {
            TerrainGenerationSettings settings = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(
                "Assets/Game/Configuration/DefaultTerrainSettings.asset");

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Width, Is.GreaterThan(0));
            Assert.That(settings.Length, Is.GreaterThan(0));
            Assert.That(settings.Width, Is.EqualTo(48));
            Assert.That(settings.Length, Is.EqualTo(48));
            Assert.That(settings.CellSize, Is.GreaterThan(0f));
            Assert.That(settings.Seed, Is.EqualTo(8128));
        }

        [Test]
        public void WorldVisualConfiguration_ConnectsGeneratedCutoutArt()
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
            Assert.That(visuals.LandingPodExteriorSprite, Is.Not.Null);
            Assert.That(visuals.LandingPodExteriorHeight, Is.GreaterThan(0f));
            int cellWidth = visuals.PlayerAnimationSheet.width / visuals.PlayerFramesPerDirection;
            int cellHeight = visuals.PlayerAnimationSheet.height / 4;
            for (int row = 0; row < 4; row++)
            {
                Vector2 directionPivot = visuals.PlayerFramePivots[row * 8];
                for (int column = 0; column < 8; column++)
                {
                    int index = row * 8 + column;
                    Assert.That(visuals.PlayerFrameRects[index], Is.EqualTo(new Rect(
                        column * cellWidth,
                        (3 - row) * cellHeight,
                        cellWidth,
                        cellHeight)));
                    Assert.That(visuals.PlayerFramePivots[index], Is.EqualTo(directionPivot),
                        $"Direction row {row} must use one stable pivot across all frames.");
                }
            }
            Assert.That(rock, Is.Not.Null);
            Assert.That(rock.WorldSprite, Is.Not.Null);
        }
    }
}
