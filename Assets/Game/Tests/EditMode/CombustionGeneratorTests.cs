using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Oxygen.Definitions;
using PlanetSurvival.Power.Definitions;
using PlanetSurvival.Power.Domain;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Tests.EditMode
{
    public sealed class CombustionGeneratorTests
    {
        private readonly List<Object> _assets = new();
        private ItemDefinition _hydrogen;
        private ItemDefinition _methane;
        private ItemDefinition _oxygen;
        private ItemDefinition _water;
        private ItemDefinition _carbonDioxide;
        private CombustionGeneratorDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _hydrogen = Item("hydrogen");
            _methane = Item("methane");
            _oxygen = Item("oxygen");
            _water = Item("water");
            _carbonDioxide = Item("carbon_dioxide");
            _definition = ScriptableObject.CreateInstance<CombustionGeneratorDefinition>();
            _definition.Configure(_oxygen, 12, 20, 5,
                new CombustionFuelRecipe(_hydrogen, 2, 1, 4f, 15f, .4f, _water, 2),
                new CombustionFuelRecipe(_methane, 1, 2, 6f, 20f, .45f, _water, 2, _carbonDioxide, 1));
            _assets.Add(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Hydrogen_RequiresOxygenAndConsumesTwoFuelPerReaction()
        {
            var generator = new CombustionGenerator(_definition);
            Assert.That(generator.InsertInputItems(_hydrogen, 2), Is.EqualTo(2));
            generator.Advance(10f);
            Assert.That(generator.CurrentPowerWatts, Is.Zero);
            generator.InsertInputItems(_oxygen, 1);
            Assert.That(generator.CurrentPowerWatts, Is.EqualTo(1.5f).Within(.001f));
            generator.Advance(3.9f);
            Assert.That(generator.CurrentPowerWatts, Is.EqualTo(1.5f).Within(.001f));
            generator.Advance(.1f);
            Assert.That(generator.FuelQuantity, Is.Zero);
            Assert.That(generator.OxygenQuantity, Is.Zero);
            Assert.That(generator.CurrentPowerWatts, Is.Zero);
            Assert.That(generator.Byproducts.GetQuantity(_water.ItemId), Is.EqualTo(2));
        }

        [Test]
        public void Methane_UsesItsOwnRateAndEfficiencyAndProducesBothExhausts()
        {
            var generator = new CombustionGenerator(_definition);
            generator.InsertInputItems(_methane, 1);
            generator.InsertInputItems(_oxygen, 2);
            Assert.That(generator.CurrentPowerWatts, Is.EqualTo(1.5f).Within(.001f));
            generator.Advance(6f);
            Assert.That(generator.CurrentPowerWatts, Is.Zero);
            Assert.That(generator.Byproducts.GetQuantity(_water.ItemId), Is.EqualTo(2));
            Assert.That(generator.Byproducts.GetQuantity(_carbonDioxide.ItemId), Is.EqualTo(1));
        }

        [Test]
        public void FullExhaustBuffer_StopsBeforeConsumingInputs()
        {
            var generator = new CombustionGenerator(_definition);
            generator.InsertInputItems(_methane, 2);
            generator.InsertInputItems(_oxygen, 4);
            generator.Advance(12f);
            Assert.That(generator.ByproductQuantity, Is.EqualTo(3));
            Assert.That(generator.FuelQuantity, Is.EqualTo(1));
            Assert.That(generator.OxygenQuantity, Is.EqualTo(2));
            Assert.That(generator.CurrentPowerWatts, Is.Zero);
        }

        [Test]
        public void NoPowerConnection_DoesNotStopFuelConsumption()
        {
            _definition.Configure(_oxygen, 12, 20, 20,
                new CombustionFuelRecipe(_hydrogen, 2, 1, 4f, 15f, .4f, _water, 2));
            var generator = new CombustionGenerator(_definition);
            generator.InsertInputItems(_hydrogen, 6);
            generator.InsertInputItems(_oxygen, 3);

            generator.Advance(12f);

            Assert.That(generator.FuelQuantity, Is.Zero);
            Assert.That(generator.OxygenQuantity, Is.Zero);
            Assert.That(generator.Byproducts.GetQuantity(_water.ItemId), Is.EqualTo(6));
            Assert.That(generator.CurrentPowerWatts, Is.Zero);
        }

        [Test]
        public void SourceAndByproductSelections_KeepTheirItemAndPostAssignments()
        {
            var generator = new CombustionGenerator(_definition);
            generator.ConfigureFuelInput("post:fuel", _methane);
            generator.ConfigureOxygenInput("post:oxygen");
            generator.ConfigureOutputPost(_water, "post:water");
            generator.ConfigureOutputPost(_carbonDioxide, "post:co2");

            Assert.That(generator.FuelInputPostId, Is.EqualTo("post:fuel"));
            Assert.That(generator.FuelInputItemId, Is.EqualTo(_methane.ItemId));
            Assert.That(generator.OxygenInputPostId, Is.EqualTo("post:oxygen"));
            Assert.That(generator.GetOutputPostId(_water), Is.EqualTo("post:water"));
            Assert.That(generator.GetOutputPostId(_carbonDioxide), Is.EqualTo("post:co2"));

            generator.ConfigureOutputPost(_water, string.Empty);
            Assert.That(generator.GetOutputPostId(_water), Is.Empty);
            Assert.That(generator.GetOutputPostId(_carbonDioxide), Is.EqualTo("post:co2"));
        }

        [Test]
        public void AuthoredAssets_AreLinkedIntoBuildingCatalogAndElectrolyzer()
        {
            const string root = "Assets/Game/Configuration/";
            BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(
                root + "DefaultBuildingCatalog.asset");
            BuildableDefinition buildable = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(
                root + "CombustionGeneratorBuildable.asset");
            ElectrolyzerDefinition electrolyzer = AssetDatabase.LoadAssetAtPath<ElectrolyzerDefinition>(
                root + "Electrolyzer.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(buildable, Is.Not.Null);
            Assert.That(buildable.IsValid(out string error), Is.True, error);
            Assert.That(catalog.Buildables, Does.Contain(buildable));
            Assert.That(buildable.WorldSprite, Is.Not.Null);
            Assert.That(buildable.MenuIcon, Is.SameAs(buildable.WorldSprite));
            Assert.That(electrolyzer.HydrogenItem, Is.Not.Null);
            Assert.That(buildable.CombustionGenerator.TryGetRecipe(electrolyzer.HydrogenItem, out _), Is.True);
        }

        private ItemDefinition Item(string id)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(id, id, 1, 10, false, true);
            _assets.Add(item);
            return item;
        }
    }
}
