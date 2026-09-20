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
        private PlanterBoxDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _canister = ScriptableObject.CreateInstance<ItemDefinition>();
            _canister.Configure("co2", "CO2 Canister", 1, 20, false, true);
            _definition = ScriptableObject.CreateInstance<PlanterBoxDefinition>();
            _definition.Configure(10000, 1000, 500f, 25f, 500f, 10f, 2f, 1f, _canister, 50f);
            _assets.Add(_canister);
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
            Assert.That(planter.State, Is.EqualTo(PlanterBoxState.NeedsCarbonDioxide));
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
    }
}
