using NUnit.Framework;
using PlanetSurvival.World.Generation;
using UnityEditor;

namespace PlanetSurvival.Tests
{
    public sealed class FormalSceneConfigurationTests
    {
        private static readonly string[] ExpectedScenePaths =
        {
            "Assets/Game/Scenes/Bootstrap.unity",
            "Assets/Game/Scenes/MainMenu.unity",
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
        public void DefaultTerrainSettings_HasValidPlayableDimensions()
        {
            TerrainGenerationSettings settings = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(
                "Assets/Game/Configuration/DefaultTerrainSettings.asset");

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Width, Is.GreaterThan(0));
            Assert.That(settings.Length, Is.GreaterThan(0));
            Assert.That(settings.CellSize, Is.GreaterThan(0f));
            Assert.That(settings.UndergroundDepth, Is.GreaterThan(0f));
        }
    }
}
