using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class HydroponicsTests
    {
        private const float GrowthGameHours = 20f;
        private const int PlantingWaterMilliliters = 1500;
        private const int HarvestQuantity = 4;
        private const double GameHoursPerDay = 24d;

        private readonly List<Object> _createdAssets = new();
        private ItemDefinition _potato;
        private CropDefinition _potatoCrop;

        [SetUp]
        public void SetUp()
        {
            _potato = CreateItem("potato", "Potato", 1, 20);
            _potatoCrop = ScriptableObject.CreateInstance<CropDefinition>();
            _potatoCrop.Configure("potato_crop", "Potato", _potato, 1, _potato, HarvestQuantity,
                GrowthGameHours, PlantingWaterMilliliters);
            _createdAssets.Add(_potatoCrop);
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
        public void Crop_ReturnsMoreThanItSows()
        {
            Assert.That(_potatoCrop.IsValid(out string error), Is.True, error);
            Assert.That(_potatoCrop.HarvestQuantity, Is.GreaterThan(_potatoCrop.SeedQuantity),
                "Farming only closes the food loop when a harvest beats its own seed cost.");
        }

        [Test]
        public void Plant_PaysTheSeedAndTheWaterUpFront()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 3);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);

            FarmingResult result = slot.Plant(_potatoCrop, inventory, water, 2d);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(slot.IsPlanted, Is.True);
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(2));
            Assert.That(water.CurrentMilliliters, Is.EqualTo(8000 - PlantingWaterMilliliters));
        }

        [Test]
        public void Plant_WithoutWater_KeepsTheSeed()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var water = new LiquidContainer(20000, 500);
            var slot = new HydroponicsSlot(0);

            FarmingResult result = slot.Plant(_potatoCrop, inventory, water, 0d);

            Assert.That(result.Failure, Is.EqualTo(FarmingFailure.NotEnoughWater));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1));
            Assert.That(water.CurrentMilliliters, Is.EqualTo(500));
            Assert.That(slot.IsPlanted, Is.False);
        }

        [Test]
        public void Plant_WithoutASeed_ReportsTheMissingSeed()
        {
            var inventory = new InventoryModel(30, 20);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);

            FarmingResult result = slot.Plant(_potatoCrop, inventory, water, 0d);

            Assert.That(result.Failure, Is.EqualTo(FarmingFailure.MissingSeed));
            Assert.That(water.CurrentMilliliters, Is.EqualTo(8000));
        }

        [Test]
        public void Plant_IntoAnOccupiedTray_IsRefused()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 2);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);
            slot.Plant(_potatoCrop, inventory, water, 0d);

            FarmingResult result = slot.Plant(_potatoCrop, inventory, water, 0d);

            Assert.That(result.Failure, Is.EqualTo(FarmingFailure.SlotOccupied));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1));
        }

        [Test]
        public void Crop_RipensOnExpeditionTimeSoItGrowsWhileThePlayerIsOutside()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);
            slot.Plant(_potatoCrop, inventory, water, 3d);
            double halfway = 3d + GrowthGameHours / GameHoursPerDay * .5d;

            Assert.That(slot.IsRipe(halfway), Is.False);
            Assert.That(slot.Progress(halfway), Is.EqualTo(.5f).Within(.01f));
            Assert.That(slot.RemainingGameHours(halfway), Is.EqualTo(GrowthGameHours * .5f).Within(.01f));

            double ripeDay = 3d + GrowthGameHours / GameHoursPerDay;

            Assert.That(slot.IsRipe(ripeDay), Is.True);
            Assert.That(slot.RemainingGameHours(ripeDay), Is.Zero);
        }

        [Test]
        public void Harvest_BeforeTheCropIsRipe_IsRefused()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);
            slot.Plant(_potatoCrop, inventory, water, 0d);

            FarmingResult result = slot.Harvest(inventory, .1d);

            Assert.That(result.Failure, Is.EqualTo(FarmingFailure.NotRipe));
            Assert.That(inventory.GetQuantity("potato"), Is.Zero);
            Assert.That(slot.IsPlanted, Is.True);
        }

        [Test]
        public void Harvest_MovesTheProduceIntoTheBackpackAndFreesTheTray()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);
            slot.Plant(_potatoCrop, inventory, water, 0d);

            FarmingResult result = slot.Harvest(inventory, 1d);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(HarvestQuantity));
            Assert.That(slot.IsPlanted, Is.False);
        }

        [Test]
        public void Harvest_WithAFullBackpack_LeavesTheProduceInTheTray()
        {
            var inventory = new InventoryModel(6, 20);
            inventory.Add(_potato, 1);
            var water = new LiquidContainer(20000, 8000);
            var slot = new HydroponicsSlot(0);
            slot.Plant(_potatoCrop, inventory, water, 0d);
            ItemDefinition ballast = CreateItem("ballast", "Ballast", 1, 10);
            inventory.Add(ballast, 4);

            FarmingResult result = slot.Harvest(inventory, 1d);

            Assert.That(result.Failure, Is.EqualTo(FarmingFailure.InventoryFull));
            Assert.That(slot.IsPlanted, Is.True);
            Assert.That(slot.IsRipe(1d), Is.True);
        }

        [Test]
        public void Rack_ReportsRipeTraysAndTheNextFreeTray()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 2);
            var water = new LiquidContainer(20000, 8000);
            var rack = new HydroponicsRack(3);

            Assert.That(rack.FirstEmptySlot(), Is.SameAs(rack.Slots[0]));

            rack.Slots[0].Plant(_potatoCrop, inventory, water, 0d);
            rack.Slots[1].Plant(_potatoCrop, inventory, water, 1d);

            Assert.That(rack.FirstEmptySlot(), Is.SameAs(rack.Slots[2]));
            Assert.That(rack.RipeCount(0d), Is.Zero);
            Assert.That(rack.RipeCount(1d), Is.EqualTo(1));
            Assert.That(rack.RipeCount(3d), Is.EqualTo(2));

            rack.Clear();

            Assert.That(rack.RipeCount(3d), Is.Zero);
            Assert.That(rack.FirstEmptySlot(), Is.SameAs(rack.Slots[0]));
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
