using System;
using System.Collections.Generic;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Cooking.Domain;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Core.Flow
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>Runtime state that must survive scene changes during one expedition.</summary>
    public sealed class GameSessionState
    {
        public const int WaterBottleCapacityMilliliters = 500;
        public const int LandingPodWaterCapacityMilliliters = 20000;

        /// <summary>
        /// Water the pod lands with. It covers roughly two days of drinking and hydroponics together, so
        /// the first trip for ice has to happen long before the rescue window opens.
        /// </summary>
        public const int InitialLandingPodWaterMilliliters = 8000;
        public const float SpaceSuitOxygenCapacityLiters = 600f;
        public const float InitialSpaceSuitOxygenLiters = SpaceSuitOxygenCapacityLiters;
        public const float LandingPodOxygenCapacityLiters = 36000f;
        public const float InitialLandingPodOxygenLiters = LandingPodOxygenCapacityLiters;
        public const int PlayerInventorySlots = 20;
        public const int PlayerInventoryCapacity = 30;
        public const int RefrigeratorSlots = 12;
        public const int RefrigeratorCapacity = 60;
        public const int CargoStorageSlots = 30;
        public const int CargoStorageCapacity = 300;
        public const int InitialEnergyBarCount = 12;
        public const int InitialPotatoCount = 12;
        public const int InitialAluminumAlloyCount = 20;

        /// <summary>Growing trays the habitat rack offers. Two feed one explorer; the third is headroom.</summary>
        public const int HydroponicsSlotCount = 3;

        /// <summary>World units per placement cell. One metre keeps structures aligned with the terrain grid.</summary>
        public const float BuildGridCellSize = 1f;

        private const double DefaultRealSecondsPerGameDay = 600d;
        private const int DefaultRescueDay = 30;

        private readonly Dictionary<string, CookingProcess> _cookingProcesses = new(StringComparer.Ordinal);
        private double _realSecondsPerGameDay = DefaultRealSecondsPerGameDay;
        private int _rescueDay = DefaultRescueDay;
        private GameTimeModel _time;

        public GameSessionState()
        {
            SpaceSuit = new SpaceSuitResources(
                new OxygenReservoir(SpaceSuitOxygenCapacityLiters, InitialSpaceSuitOxygenLiters),
                new LiquidContainer(WaterBottleCapacityMilliliters));
            LandingPodWaterSupply = new LiquidContainer(
                LandingPodWaterCapacityMilliliters,
                InitialLandingPodWaterMilliliters);
            LandingPodOxygenSupply = new OxygenReservoir(
                LandingPodOxygenCapacityLiters,
                InitialLandingPodOxygenLiters);
            SurvivalStats = new SurvivalStats();
            PlayerInventory = new InventoryModel(PlayerInventoryCapacity, PlayerInventorySlots);
            RefrigeratorStorage = new InventoryModel(RefrigeratorCapacity, RefrigeratorSlots);
            CargoStorage = new InventoryModel(CargoStorageCapacity, CargoStorageSlots);
            Buildings = new BuildingService(PlayerInventory, new BuildGrid(BuildGridCellSize));
            WaterProcessor = new WaterProcessor();
            Hydroponics = new HydroponicsRack(HydroponicsSlotCount);
        }

        public SpaceSuitResources SpaceSuit { get; }
        // Compatibility alias: the existing drinking-water interactions now operate on the suit reservoir.
        public LiquidContainer WaterBottle => SpaceSuit.Water;
        public LiquidContainer LandingPodWaterSupply { get; }
        public OxygenReservoir LandingPodOxygenSupply { get; }
        public SurvivalStats SurvivalStats { get; }
        public InventoryModel PlayerInventory { get; }
        public InventoryModel RefrigeratorStorage { get; }
        public InventoryModel CargoStorage { get; }

        /// <summary>
        /// Everything the player has placed on the surface, plus the grid those cells belong to. It lives
        /// here so a half-finished structure is still standing after a trip inside the landing pod.
        /// </summary>
        public BuildingService Buildings { get; }

        /// <summary>The cargo-deck machine that filters gathered ice into drinkable pod water.</summary>
        public WaterProcessor WaterProcessor { get; }

        /// <summary>The habitat growing trays. They ripen on expedition time, including while outside.</summary>
        public HydroponicsRack Hydroponics { get; }

        /// <summary>
        /// Time elapsed in this expedition. Every scene shares it, so walking into the landing pod no
        /// longer rewinds the day counter, the rescue countdown, or anything growing on a timestamp.
        /// </summary>
        public GameTimeModel Time => _time ??= new GameTimeModel(_realSecondsPerGameDay, _rescueDay);

        /// <summary>
        /// Applies the planet's day length and rescue day to the expedition clock. The first caller of an
        /// expedition decides them; later callers only read the running clock, because a scene load must
        /// never restart elapsed time.
        /// </summary>
        public GameTimeModel ConfigureTime(float realSecondsPerGameDay, int rescueDay)
        {
            if (_time == null)
            {
                _realSecondsPerGameDay = Math.Max(1f, realSecondsPerGameDay);
                _rescueDay = Math.Max(1, rescueDay);
                return Time;
            }

            bool differs = !Mathf.Approximately((float)_realSecondsPerGameDay, realSecondsPerGameDay)
                           || _rescueDay != rescueDay;
            if (differs)
            {
                Debug.LogWarning(
                    $"The expedition clock is already running at {_realSecondsPerGameDay}s per day with rescue on " +
                    $"day {_rescueDay}; the settings of this scene ({realSecondsPerGameDay}s, day {rescueDay}) were " +
                    "ignored so elapsed time is not lost.");
            }

            return _time;
        }

        /// <summary>
        /// The cooking slot of one station, created on first use. It lives here rather than on the station
        /// component so a dish keeps cooking while the player is on another deck.
        /// </summary>
        public CookingProcess GetCookingProcess(string stationId)
        {
            if (string.IsNullOrWhiteSpace(stationId))
            {
                throw new ArgumentException("A cooking station needs an ID.", nameof(stationId));
            }

            if (!_cookingProcesses.TryGetValue(stationId, out CookingProcess process))
            {
                process = new CookingProcess(stationId);
                _cookingProcesses.Add(stationId, process);
            }

            return process;
        }

        public void Reset()
        {
            foreach (CookingProcess process in _cookingProcesses.Values)
            {
                process.Clear();
            }

            Buildings.Clear();
            WaterProcessor.Clear();
            Hydroponics.Clear();
            // A new expedition starts on day one; the configured day length and rescue day are kept.
            _time = new GameTimeModel(_realSecondsPerGameDay, _rescueDay);
            WaterBottle.Reset(0);
            SpaceSuit.Oxygen.Reset(InitialSpaceSuitOxygenLiters);
            LandingPodWaterSupply.Reset(InitialLandingPodWaterMilliliters);
            LandingPodOxygenSupply.Reset(InitialLandingPodOxygenLiters);
            SurvivalStats.Reset();
            PlayerInventory.Clear();
            RefrigeratorStorage.Clear();
            CargoStorage.Clear();
        }

        public InventoryOperationResult Reset(ItemDefinition startingCargoItem, int quantity)
        {
            return Reset(new[] { new InventoryItemAmount(startingCargoItem, quantity) });
        }

        public InventoryOperationResult Reset(IReadOnlyList<InventoryItemAmount> startingCargo)
        {
            Reset();
            return CargoStorage.ApplyTransaction(null, startingCargo);
        }
    }
}
