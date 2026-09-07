using NUnit.Framework;
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
