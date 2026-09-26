using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Oxygen.Definitions;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class ElectrolyzerTests
    {
        private readonly List<Object> _assets = new();
        private ItemDefinition _ice;
        private ItemDefinition _oxygen;
        private ElectrolyzerDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _ice = ScriptableObject.CreateInstance<ItemDefinition>();
            _ice.Configure("ice_chunk", "Ice Chunk", 1, 20, false, true);
            _oxygen = ScriptableObject.CreateInstance<ItemDefinition>();
            _oxygen.Configure("oxygen", "Oxygen", 1, 10, false, true);
            _definition = ScriptableObject.CreateInstance<ElectrolyzerDefinition>();
            // 10 L/s of oxygen from 5 mL of water and 0.2 units each, with 2 L of hydrogen alongside.
            _definition.Configure(_ice, _oxygen, 20000, 1000, 5f, 40f, .2f, 10f, 600f, 2f, 1200f, 100f, 10, 1f);
            _assets.Add(_ice);
            _assets.Add(_oxygen);
            _assets.Add(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assets.Count; i++) Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        [Test]
        public void Advance_NeedsBothWaterAndPowerBeforeAnyGasIsProduced()
        {
            var electrolyzer = new Electrolyzer(_definition);

            Assert.That(electrolyzer.State, Is.EqualTo(ElectrolyzerState.NeedsWater));
            electrolyzer.ReceiveWater(1000);
            Assert.That(electrolyzer.State, Is.EqualTo(ElectrolyzerState.NeedsPower));
            electrolyzer.Advance(1f);
            Assert.That(electrolyzer.StoredOxygenLiters, Is.Zero);
            Assert.That(electrolyzer.StoredHydrogenLiters, Is.Zero);

            electrolyzer.ReceiveElectricity(2f);
            Assert.That(electrolyzer.State, Is.EqualTo(ElectrolyzerState.Producing));
            electrolyzer.Advance(1f);

            Assert.That(electrolyzer.StoredOxygenLiters, Is.EqualTo(10f).Within(.001f));
            Assert.That(electrolyzer.StoredHydrogenLiters, Is.EqualTo(20f).Within(.001f));
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.EqualTo(950));
            Assert.That(electrolyzer.StoredElectricity, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void Advance_IsLimitedByWhicheverInputRunsOutFirst()
        {
            var electrolyzer = new Electrolyzer(_definition);
            electrolyzer.ReceiveWater(10);
            electrolyzer.ReceiveElectricity(40f);

            electrolyzer.Advance(1f);

            Assert.That(electrolyzer.StoredOxygenLiters, Is.EqualTo(2f).Within(.001f),
                "Two litres is all 10 mL of feed water can yield, however much power is buffered.");
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.Zero);
            Assert.That(electrolyzer.StoredElectricity, Is.EqualTo(39.6f).Within(.001f));
        }

        [Test]
        public void Advance_SplitAcrossManySmallStepsMatchesOneLargeStep()
        {
            var stepped = new Electrolyzer(_definition);
            var single = new Electrolyzer(_definition);
            foreach (Electrolyzer machine in new[] { stepped, single })
            {
                machine.ReceiveWater(5000);
                machine.ReceiveElectricity(40f);
            }

            for (int i = 0; i < 60; i++) stepped.Advance(1f / 60f);
            single.Advance(1f);

            Assert.That(stepped.StoredOxygenLiters, Is.EqualTo(single.StoredOxygenLiters).Within(.001f));
            Assert.That(stepped.StoredWaterMilliliters, Is.EqualTo(single.StoredWaterMilliliters).Within(1),
                "Fractional millilitres must stay inside the machine instead of rounding away each frame.");
        }

        [Test]
        public void Advance_StopsWhenTheHydrogenVentTankIsFullAndResumesAfterVenting()
        {
            var electrolyzer = new Electrolyzer(_definition);
            electrolyzer.ReceiveWater(20000);
            FillHydrogen(electrolyzer);

            Assert.That(electrolyzer.State, Is.EqualTo(ElectrolyzerState.HydrogenStorageFull));
            Assert.That(electrolyzer.RequestedElectricity(1f), Is.Zero,
                "A stalled machine must not keep pulling power off the grid.");
            float oxygenWhileStalled = electrolyzer.StoredOxygenLiters;
            electrolyzer.ReceiveElectricity(40f);
            electrolyzer.Advance(1f);
            Assert.That(electrolyzer.StoredOxygenLiters, Is.EqualTo(oxygenWhileStalled).Within(.001f));

            Assert.That(electrolyzer.VentHydrogen(), Is.EqualTo(1200f).Within(.001f));
            electrolyzer.Advance(1f);
            Assert.That(electrolyzer.StoredOxygenLiters, Is.GreaterThan(oxygenWhileStalled));
        }

        [Test]
        public void Advance_BottlesOnlyTheOxygenPastAFullGasBufferAndThenStalls()
        {
            var electrolyzer = new Electrolyzer(_definition);

            RunUntilBottled(electrolyzer, 1);

            Assert.That(electrolyzer.StoredOxygenItems, Is.EqualTo(1));
            Assert.That(electrolyzer.StoredOxygenLiters, Is.GreaterThanOrEqualTo(500f),
                "Bottling must leave the buffer nearly full, or a suit could never be topped up from it.");

            RunUntilBottled(electrolyzer, _definition.OxygenItemCapacity);
            // With the bin full the buffer fills too, and that is what finally stops electrolysis.
            for (int i = 0; i < 20; i++)
            {
                electrolyzer.ReceiveWater(1000);
                electrolyzer.ReceiveElectricity(40f);
                electrolyzer.VentHydrogen();
                electrolyzer.Advance(1f);
            }

            Assert.That(electrolyzer.StoredOxygenItems, Is.EqualTo(_definition.OxygenItemCapacity));
            Assert.That(electrolyzer.StoredOxygenLiters,
                Is.EqualTo(_definition.OxygenCapacityLiters).Within(.001f));
            Assert.That(electrolyzer.State, Is.EqualTo(ElectrolyzerState.OxygenStorageFull));
            Assert.That(electrolyzer.RequestedElectricity(1f), Is.Zero);
        }

        [Test]
        public void TransferOxygenTo_FillsASuitFromTheGasBufferAndNeverPastItsCapacity()
        {
            var electrolyzer = new Electrolyzer(_definition);
            Produce(electrolyzer, 300f);
            var suit = new OxygenReservoir(600f, 500f);

            float filled = electrolyzer.TransferOxygenTo(suit);

            Assert.That(filled, Is.EqualTo(100f).Within(.001f));
            Assert.That(suit.CurrentLiters, Is.EqualTo(600f).Within(.001f));
            Assert.That(electrolyzer.StoredOxygenLiters, Is.EqualTo(200f).Within(.001f));
        }

        [Test]
        public void LoadIceItems_MeltsWholeChunksAndTakesOnlyWhatTheTankHolds()
        {
            var electrolyzer = new Electrolyzer(_definition);
            var inventory = new InventoryModel(100, 10);
            inventory.Add(_ice, 25);

            int melted = electrolyzer.LoadIceItems(25, inventory);

            Assert.That(melted, Is.EqualTo(20), "The 20 L tank takes twenty 1 L chunks and no more.");
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.EqualTo(20000));
            Assert.That(inventory.GetQuantity("ice_chunk"), Is.EqualTo(5));
            Assert.That(electrolyzer.LoadIceItems(1, inventory), Is.Zero);
        }

        [Test]
        public void InsertInputItems_AcceptsIceFromAutomationAndRejectsEverythingElse()
        {
            var electrolyzer = new Electrolyzer(_definition);

            Assert.That(electrolyzer.AcceptableInputItems(_oxygen, 4), Is.Zero);
            Assert.That(electrolyzer.InsertInputItems(_oxygen, 4), Is.Zero);
            Assert.That(electrolyzer.InsertInputItems(_ice, 3), Is.EqualTo(3));
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.EqualTo(3000));
        }

        [Test]
        public void Extract_RateLimitsBottledOxygenForAutomation()
        {
            var electrolyzer = new Electrolyzer(_definition);
            RunUntilBottled(electrolyzer, 3);

            Assert.That(electrolyzer.Extract(3, 1f), Is.EqualTo(1),
                "One bottle per second is the authored automation throughput.");
            Assert.That(electrolyzer.Extract(3, 0f), Is.Zero);
            Assert.That(electrolyzer.StoredOxygenItems, Is.EqualTo(2));
        }

        [Test]
        public void WaterTap_PoursIntoTheTankAndDrawsBackOutWithoutCreatingWater()
        {
            var electrolyzer = new Electrolyzer(_definition);
            var bottle = new LiquidContainer(500, 500);

            Assert.That(electrolyzer.TransferWaterFrom(bottle, 500), Is.EqualTo(500));
            Assert.That(bottle.CurrentMilliliters, Is.Zero);
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.EqualTo(500));

            Assert.That(electrolyzer.DrawWaterTo(bottle, 900), Is.EqualTo(500),
                "The tap is clamped by both the feed tank and the carried bottle.");
            Assert.That(bottle.CurrentMilliliters, Is.EqualTo(500));
            Assert.That(electrolyzer.StoredWaterMilliliters, Is.Zero);
            Assert.That(electrolyzer.DrawWaterTo(bottle, 100), Is.Zero);
        }

        [Test]
        public void ReceiveElectricity_NeverStoresMoreThanTheInputBufferHolds()
        {
            var electrolyzer = new Electrolyzer(_definition);

            Assert.That(electrolyzer.ReceiveElectricity(100f), Is.EqualTo(40f).Within(.001f));
            Assert.That(electrolyzer.ReceiveElectricity(float.NaN), Is.Zero);
            Assert.That(electrolyzer.StoredElectricity, Is.EqualTo(40f).Within(.001f));
        }

        /// <summary>Runs the machine, re-feeding it, until it holds at least <paramref name="items"/> bottles.</summary>
        private static void RunUntilBottled(Electrolyzer electrolyzer, int items)
        {
            for (int i = 0; i < 10000 && electrolyzer.StoredOxygenItems < items; i++)
            {
                electrolyzer.ReceiveWater(1000);
                electrolyzer.ReceiveElectricity(40f);
                electrolyzer.VentHydrogen();
                electrolyzer.Advance(1f);
            }
        }

        private static void Produce(Electrolyzer electrolyzer, float oxygenLiters)
        {
            while (electrolyzer.StoredOxygenLiters < oxygenLiters)
            {
                electrolyzer.ReceiveWater(1000);
                electrolyzer.ReceiveElectricity(40f);
                electrolyzer.Advance(1f);
            }
        }

        private static void FillHydrogen(Electrolyzer electrolyzer)
        {
            while (electrolyzer.RemainingHydrogenCapacity > .0001f)
            {
                electrolyzer.ReceiveElectricity(40f);
                electrolyzer.Advance(1f);
            }
        }
    }
}
