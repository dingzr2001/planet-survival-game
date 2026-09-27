using System.Collections.Generic;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Oxygen.Definitions;
using PlanetSurvival.Power.Definitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetSurvival.Editor
{
    public static class CombustionGeneratorSetup
    {
        private const string Directory = "Assets/Game/Configuration/";
        private const string CatalogPath = Directory + "DefaultBuildingCatalog.asset";
        private const string BootstrapPath = "Assets/Game/Scenes/Bootstrap.unity";

        [MenuItem("Planet Survival/Setup Combustion Generator")]
        public static void Setup()
        {
            BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("Create the default building catalog before setting up the combustion generator.");
                return;
            }
            EnsureContent(catalog);
            ConfigureBootstrapScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Combustion generator, fuels, byproducts and starting supplies are ready.");
        }

        public static ItemDefinition GetOrCreateHydrogen() => GetOrCreateItem(
            "Hydrogen", "hydrogen", "Hydrogen", "A 100 L bottle of electrolyzer hydrogen.");

        public static BuildableDefinition EnsureContent(BuildingCatalog catalog)
        {
            ItemDefinition hydrogen = GetOrCreateHydrogen();
            ItemDefinition methane = GetOrCreateItem("MethaneCanister", "methane_canister",
                "Methane Canister", "Sealed methane fuel for a combustion generator.");
            ItemDefinition water = GetOrCreateItem("WaterCanister", "water_canister",
                "Water Canister", "Condensed combustion water stored as a portable item.");
            ItemDefinition oxygen = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "Oxygen.asset");
            ItemDefinition carbonDioxide = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "CarbonDioxideCanister.asset");
            ItemDefinition petroleum = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "PetroleumCanister.asset");
            ItemDefinition alloy = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "AluminumAlloy.asset");
            ItemDefinition plastic = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "PlasticSheet.asset");
            if (oxygen == null || carbonDioxide == null || petroleum == null || alloy == null || plastic == null)
            {
                Debug.LogError("Combustion generator setup needs the standard oxygen, CO₂, petroleum and building materials.");
                return null;
            }

            string definitionPath = Directory + "CombustionGenerator.asset";
            CombustionGeneratorDefinition definition =
                AssetDatabase.LoadAssetAtPath<CombustionGeneratorDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CombustionGeneratorDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            definition.Configure(oxygen, 12, 20, 20,
                new CombustionFuelRecipe(hydrogen, 2, 1, 4f, 15f, .4f, water, 2),
                new CombustionFuelRecipe(methane, 1, 2, 6f, 20f, .45f, water, 2, carbonDioxide, 1),
                new CombustionFuelRecipe(petroleum, 1, 3, 7f, 25f, .32f, water, 2, carbonDioxide, 2));
            EditorUtility.SetDirty(definition);

            string buildablePath = Directory + "CombustionGeneratorBuildable.asset";
            BuildableDefinition buildable = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(buildablePath);
            if (buildable == null)
            {
                buildable = ScriptableObject.CreateInstance<BuildableDefinition>();
                AssetDatabase.CreateAsset(buildable, buildablePath);
            }
            buildable.Configure("combustion_generator", "Combustion Generator", Vector2Int.one, 8f,
                new CraftingItemAmount(alloy, 6), new CraftingItemAmount(plastic, 3));
            buildable.ConfigureDescription("Burns bottled fuel with oxygen. Unused power is wasted; connect a power pole to use it and a transfer post for exhaust.");
            Sprite sprite = WorldArtSetup.ImportBuildingSprite("CombustionGenerator");
            buildable.ConfigurePresentation(sprite, 1.3f, new Color(.5f, .38f, .3f));
            buildable.ConfigureIcon(sprite);
            buildable.ConfigureCombustionGenerator(definition);
            EditorUtility.SetDirty(buildable);

            var buildables = new List<BuildableDefinition>(catalog.Buildables);
            if (!buildables.Contains(buildable)) buildables.Add(buildable);
            catalog.Configure(buildables.ToArray());
            EditorUtility.SetDirty(catalog);

            ElectrolyzerDefinition electrolyzer =
                AssetDatabase.LoadAssetAtPath<ElectrolyzerDefinition>(Directory + "Electrolyzer.asset");
            if (electrolyzer != null)
            {
                SerializedObject serialized = new(electrolyzer);
                serialized.FindProperty("_hydrogenItem").objectReferenceValue = hydrogen;
                serialized.FindProperty("_hydrogenLitersPerItem").floatValue = 100f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(electrolyzer);
            }
            return buildable;
        }

        public static void ConfigureBootstrapScene()
        {
            if (!System.IO.File.Exists(BootstrapPath)) return;
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameFlowController flow = root.GetComponent<GameFlowController>();
                if (flow == null) continue;
                flow.ConfigureGeneratorSupplies(
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "Oxygen.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "MethaneCanister.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>(Directory + "Hydrogen.asset"));
                EditorUtility.SetDirty(flow);
                EditorSceneManager.SaveScene(scene);
                break;
            }
            EditorSceneManager.CloseScene(scene, true);
        }

        private static ItemDefinition GetOrCreateItem(string assetName, string id, string displayName,
            string description)
        {
            string path = Directory + assetName + ".asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }
            item.Configure(id, displayName, 1, 10, false, true);
            item.ConfigureDescription(description);
            EditorUtility.SetDirty(item);
            return item;
        }
    }
}
