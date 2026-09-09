using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class WaterProcessorTests
    {
        private const double GameHoursPerDay = 24d;

        private readonly List<Object> _createdAssets = new();
        private ItemDefinition _iceChunk;

        [SetUp]
        public void SetUp()
        {
            _iceChunk = CreateItem("ice_chunk", "Ice Chunk", 1, 20);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Load_TakesTheIceImmediatelyAndStartsTheBatch()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 6);
            var processor = new WaterProcessor();

            WaterProcessorResult result = processor.Load(_iceChunk, 4, inventory, 1d);

            Assert.That(result, Is.EqualTo(WaterProcessorResult.Succeeded));
            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Processing));
            Assert.That(processor.ProcessingChunks, Is.EqualTo(4));
            Assert.That(inventory.GetQuantity("ice_chunk"), Is.EqualTo(2));
            Assert.That(processor.PendingMilliliters, Is.Zero);
        }

        [Test]
        public void Load_WithoutEnoughIce_ChangesNothing()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 2);
            var processor = new WaterProcessor();

            WaterProcessorResult result = processor.Load(_iceChunk, 3, inventory, 0d);

            Assert.That(result, Is.EqualTo(WaterProcessorResult.NotEnoughIce));
            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Idle));
            Assert.That(inventory.GetQuantity("ice_chunk"), Is.EqualTo(2));
        }

        [Test]
        public void Load_AboveTheBatchLimit_IsRejected()
        {
            var inventory = new InventoryModel(60, 20);
            inventory.Add(_iceChunk, WaterProcessor.MaximumChunksPerBatch + 5);
            var processor = new WaterProcessor();

            WaterProcessorResult result = processor.Load(
                _iceChunk, WaterProcessor.MaximumChunksPerBatch + 1, inventory, 0d);

            Assert.That(result, Is.EqualTo(WaterProcessorResult.InvalidRequest));
            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Idle));
        }

        [Test]
        public void Batch_RipensOnExpeditionTimeRatherThanOnSceneTime()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 2);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 2, inventory, 5d);
            double halfway = 5d + WaterProcessor.ProcessingGameHours(2) / GameHoursPerDay * .5d;

            processor.Advance(halfway);

            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Processing));
            Assert.That(processor.Progress(halfway), Is.EqualTo(.5f).Within(.01f));

            // A player who walks back in three days later finds the same finished batch waiting.
            processor.Advance(8d);

            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Ready));
            Assert.That(processor.PendingMilliliters,
                Is.EqualTo(2 * WaterProcessor.MillilitersPerIceChunk));
            Assert.That(processor.ProcessingChunks, Is.Zero);
        }

        [Test]
        public void Collect_PoursTheProcessedWaterIntoTheTank()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 3);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 3, inventory, 0d);
            processor.Advance(1d);
            var tank = new LiquidContainer(20000, 1000);

            WaterProcessorResult result = processor.Collect(tank, out int transferred);

            Assert.That(result, Is.EqualTo(WaterProcessorResult.Succeeded));
            Assert.That(transferred, Is.EqualTo(3 * WaterProcessor.MillilitersPerIceChunk));
            Assert.That(tank.CurrentMilliliters, Is.EqualTo(1000 + transferred));
            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Idle));
            Assert.That(processor.PendingMilliliters, Is.Zero);
        }

        [Test]
        public void Collect_IntoAnAlmostFullTank_KeepsTheRestInTheMachine()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 3);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 3, inventory, 0d);
            processor.Advance(1d);
            var tank = new LiquidContainer(5000, 4200);

            WaterProcessorResult result = processor.Collect(tank, out int transferred);

            Assert.That(result, Is.EqualTo(WaterProcessorResult.Succeeded));
            Assert.That(transferred, Is.EqualTo(800));
            Assert.That(tank.CurrentMilliliters, Is.EqualTo(5000));
            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Ready));
            Assert.That(processor.PendingMilliliters, Is.EqualTo(2200));
        }

        [Test]
        public void Load_WhileWaterIsWaiting_IsRefusedSoNoBatchIsLost()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 4);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 2, inventory, 0d);
            processor.Advance(1d);

            Assert.That(processor.Load(_iceChunk, 2, inventory, 1d),
                Is.EqualTo(WaterProcessorResult.OutputWaiting));
            Assert.That(inventory.GetQuantity("ice_chunk"), Is.EqualTo(2));
        }

        [Test]
        public void Load_WhileAnotherBatchIsRunning_IsRefused()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 4);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 2, inventory, 0d);

            Assert.That(processor.Load(_iceChunk, 2, inventory, 0d),
                Is.EqualTo(WaterProcessorResult.AlreadyProcessing));
            Assert.That(inventory.GetQuantity("ice_chunk"), Is.EqualTo(2));
        }

        [Test]
        public void Collect_WhenNothingHasBeenProcessed_ReportsNothingReady()
        {
            var processor = new WaterProcessor();

            Assert.That(processor.Collect(new LiquidContainer(1000), out int transferred),
                Is.EqualTo(WaterProcessorResult.NothingReady));
            Assert.That(transferred, Is.Zero);
        }

        [Test]
        public void Clear_EmptiesTheMachineForANewExpedition()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_iceChunk, 2);
            var processor = new WaterProcessor();
            processor.Load(_iceChunk, 2, inventory, 0d);
            processor.Advance(1d);

            processor.Clear();

            Assert.That(processor.State, Is.EqualTo(WaterProcessorState.Idle));
            Assert.That(processor.PendingMilliliters, Is.Zero);
        }

        private ItemDefinition CreateItem(string itemId, string displayName, int capacity, int stackSize)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(itemId, displayName, capacity, stackSize, false, true);
            _createdAssets.Add(item);
            return item;
        }
    }
}
