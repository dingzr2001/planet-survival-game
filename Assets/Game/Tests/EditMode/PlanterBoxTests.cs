using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class PlanterBoxTests
    {
        private readonly List<Object> _assets = new();
        private ItemDefinition _canister;
        private ItemDefinition _seed;
        private ItemDefinition _potato;
        private CropDefinition _crop;
        private PlanterBoxDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _canister = ScriptableObject.CreateInstance<ItemDefinition>();
            _canister.Configure("co2", "CO2 Canister", 1, 20, false, true);
            _seed = ScriptableObject.CreateInstance<ItemDefinition>();
            _seed.Configure("potato_seed", "Seed Potato", 1, 20, false, true);
            _potato = ScriptableObject.CreateInstance<ItemDefinition>();
            _potato.Configure("potato", "Potato", 1, 20, false, true);
            _crop = ScriptableObject.CreateInstance<CropDefinition>();
            _crop.Configure("potato_crop", "Potato", _seed, 1, _potato, 4, 20f, 0);
            _crop.ConfigurePlanterGrowth(4f);
            _definition = ScriptableObject.CreateInstance<PlanterBoxDefinition>();
            _definition.Configure(10000, 1000, 500f, 25f, 500f, 10f, 2f, 1f, _canister, 50f);
            _definition.ConfigureCrops(null, _crop);
            _assets.Add(_canister);
            _assets.Add(_seed);
            _assets.Add(_potato);
            _assets.Add(_crop);
            _assets.Add(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assets.Count; i++) Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        [Test]
        public void Advance_RequiresBothInputsAndProducesBoundedOxygen()
        {
            var planter = new PlanterBox(_definition);
            planter.ReceiveWater(2000);
            planter.Advance(1f);
            Assert.That(planter.State, Is.EqualTo(PlanterBoxState.Empty));
            Assert.That(planter.StoredOxygenLiters, Is.Zero);

            planter.ReceiveCarbonDioxide(50f);
            planter.Advance(1f);

            Assert.That(planter.StoredOxygenLiters, Is.EqualTo(10f).Within(.001f));
            Assert.That(planter.StoredWaterMilliliters, Is.EqualTo(1980));
            Assert.That(planter.StoredCarbonDioxideLiters, Is.EqualTo(40f).Within(.001f));
        }

        [Test]
        public void ManualAndPipeInputsShareCapacityLimits()
        {
            var planter = new PlanterBox(_definition);
            var water = new LiquidContainer(20000, 12000);
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_canister, 12);

            Assert.That(planter.TransferWaterFrom(water, 20000), Is.EqualTo(10000));
            Assert.That(planter.ReceiveWater(1000), Is.Zero);
            Assert.That(planter.LoadCarbonDioxideItems(12, inventory), Is.EqualTo(10));
            Assert.That(planter.ReceiveCarbonDioxide(50f), Is.Zero);
            Assert.That(inventory.GetQuantity(_canister.ItemId), Is.EqualTo(2));
        }

        [Test]
        public void OxygenOutput_TransfersWithoutOverfillingTarget()
        {
            var planter = new PlanterBox(_definition);
            planter.ReceiveWater(10000);
            planter.ReceiveCarbonDioxide(500f);
            planter.Advance(20f);
            var suit = new OxygenReservoir(100f, 90f);

            float moved = planter.TransferOxygenTo(suit);

            Assert.That(moved, Is.EqualTo(10f).Within(.001f));
            Assert.That(suit.CurrentLiters, Is.EqualTo(100f).Within(.001f));
            Assert.That(planter.StoredOxygenLiters, Is.EqualTo(190f).Within(.001f));
        }

        [Test]
        public void Crop_WithoutRequiredEnvironment_DiesAndReturnsNothing()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_seed, 1);
            var planter = new PlanterBox(_definition);
            Assert.That(planter.Plant(_crop, inventory, 0d).Succeeded, Is.True);

            planter.Advance(1f, 4d / 24d);

            Assert.That(planter.IsDead, Is.True);
            Assert.That(planter.Harvest(inventory).Failure, Is.EqualTo(FarmingFailure.CropDead));
            Assert.That(inventory.GetQuantity(_seed.ItemId), Is.Zero);
            Assert.That(inventory.GetQuantity(_potato.ItemId), Is.Zero);
        }

        [Test]
        public void Crop_GrowsOnlyWithWaterAndCarbonDioxide_ThenHarvestsConfiguredYield()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_seed, 1);
            var planter = new PlanterBox(_definition);
            planter.ReceiveWater(10000);
            planter.ReceiveCarbonDioxide(500f);
            planter.Plant(_crop, inventory, 0d);

            planter.Advance(1f, 20d / 24d);
            FarmingResult result = planter.Harvest(inventory);

            Assert.That(planter.IsMature, Is.False, "Harvest clears a mature crop.");
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(inventory.GetQuantity(_potato.ItemId), Is.EqualTo(4));
        }
    }
}
