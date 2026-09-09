using System;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Water.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Turns gathered ice into drinkable water. Melting is the easy half — ground ice carries salts and
    /// dust, so the batch has to be filtered before anyone can drink it, and that is what costs time.
    /// A batch is stamped with the expedition time it will be finished at instead of being ticked down,
    /// so it keeps running while the player is on another deck or out on the surface; that is what lets
    /// one ice run pay for the days after it. The potable water stays inside the machine until it is
    /// poured, so a full tank never destroys a batch.
    /// </summary>
    public sealed class WaterProcessor
    {
        /// <summary>Water yielded by one ice chunk. One chunk is one litre, which keeps the maths readable.</summary>
        public const int MillilitersPerIceChunk = 1000;

        /// <summary>Game hours one chunk needs to purify. A full load runs a little under one game day.</summary>
        public const float ProcessingGameHoursPerIceChunk = 1.5f;

        /// <summary>Chunks one batch accepts. It caps how much of a haul can be purified unattended.</summary>
        public const int MaximumChunksPerBatch = 12;

        private const double GameHoursPerDay = 24d;

        private int _processingChunks;
        private double _startedAtDays;
        private double _readyAtDays;

        public WaterProcessorState State { get; private set; } = WaterProcessorState.Idle;

        /// <summary>Chunks in the machine right now. Zero once the batch has processed.</summary>
        public int ProcessingChunks => _processingChunks;

        /// <summary>Potable water waiting to be poured into a tank.</summary>
        public int PendingMilliliters { get; private set; }

        /// <summary>Raised whenever the state, the load, or the water held by the machine changes.</summary>
        public event Action Changed;

        /// <summary>How many chunks a batch can still take from this backpack.</summary>
        public static int MaximumLoadableChunks(ItemDefinition iceItem, InventoryModel inventory)
        {
            if (iceItem == null || inventory == null)
            {
                return 0;
            }

            return Math.Min(MaximumChunksPerBatch, inventory.GetQuantity(iceItem.ItemId));
        }

        /// <summary>Game hours a batch of this size needs to purify.</summary>
        public static float ProcessingGameHours(int chunks)
        {
            return Math.Max(0, chunks) * ProcessingGameHoursPerIceChunk;
        }

        /// <summary>
        /// Takes ice out of the backpack and starts the batch. The ice is consumed immediately, so a
        /// batch can never be paid for twice.
        /// </summary>
        public WaterProcessorResult Load(ItemDefinition iceItem, int chunks, InventoryModel inventory,
            double nowDays)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (iceItem == null || chunks < 1 || chunks > MaximumChunksPerBatch || !IsFinite(nowDays))
            {
                return WaterProcessorResult.InvalidRequest;
            }

            Advance(nowDays);
            if (State == WaterProcessorState.Ready)
            {
                return WaterProcessorResult.OutputWaiting;
            }

            if (State == WaterProcessorState.Processing)
            {
                return WaterProcessorResult.AlreadyProcessing;
            }

            if (inventory.GetQuantity(iceItem.ItemId) < chunks)
            {
                return WaterProcessorResult.NotEnoughIce;
            }

            InventoryOperationResult consumed = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(iceItem, chunks) }, null);
            if (!consumed.Succeeded)
            {
                return WaterProcessorResult.NotEnoughIce;
            }

            _processingChunks = chunks;
            _startedAtDays = nowDays;
            _readyAtDays = nowDays + ProcessingGameHours(chunks) / GameHoursPerDay;
            State = WaterProcessorState.Processing;
            Changed?.Invoke();
            // A batch with no processing time at all is already drinkable.
            Advance(nowDays);
            return WaterProcessorResult.Succeeded;
        }

        /// <summary>
        /// Settles a batch that has finished by <paramref name="nowDays"/>. Callers drive this from the
        /// scene so the machine never needs a clock of its own.
        /// </summary>
        public void Advance(double nowDays)
        {
            if (State != WaterProcessorState.Processing || !IsFinite(nowDays) || nowDays < _readyAtDays)
            {
                return;
            }

            PendingMilliliters += _processingChunks * MillilitersPerIceChunk;
            _processingChunks = 0;
            State = WaterProcessorState.Ready;
            Changed?.Invoke();
        }

        /// <summary>Share of the current batch that has processed, from 0 to 1.</summary>
        public float Progress(double nowDays)
        {
            if (State != WaterProcessorState.Processing)
            {
                return State == WaterProcessorState.Ready ? 1f : 0f;
            }

            double total = _readyAtDays - _startedAtDays;
            if (total <= 0d || !IsFinite(nowDays))
            {
                return 1f;
            }

            double progress = (nowDays - _startedAtDays) / total;
            return (float)Math.Clamp(progress, 0d, 1d);
        }

        /// <summary>Game hours left before the current batch is drinkable. Zero when nothing is processing.</summary>
        public float RemainingGameHours(double nowDays)
        {
            if (State != WaterProcessorState.Processing || !IsFinite(nowDays))
            {
                return 0f;
            }

            return (float)Math.Max(0d, (_readyAtDays - nowDays) * GameHoursPerDay);
        }

        /// <summary>
        /// Pours the potable water into a tank. A tank with too little room takes what fits and the rest
        /// stays in the machine, so water is never lost by collecting too early.
        /// </summary>
        public WaterProcessorResult Collect(LiquidContainer tank, out int transferredMilliliters)
        {
            transferredMilliliters = 0;
            if (tank == null)
            {
                throw new ArgumentNullException(nameof(tank));
            }

            if (State != WaterProcessorState.Ready || PendingMilliliters <= 0)
            {
                return WaterProcessorResult.NothingReady;
            }

            int transferable = Math.Min(PendingMilliliters, tank.RemainingCapacityMilliliters);
            if (transferable <= 0)
            {
                return WaterProcessorResult.TankFull;
            }

            // The machine is modelled as a container so the transfer stays clamped by both sides.
            var batch = new LiquidContainer(PendingMilliliters, PendingMilliliters);
            transferredMilliliters = tank.FillFrom(batch);
            PendingMilliliters = batch.CurrentMilliliters;
            if (PendingMilliliters == 0)
            {
                State = WaterProcessorState.Idle;
            }

            Changed?.Invoke();
            return WaterProcessorResult.Succeeded;
        }

        /// <summary>Empties the machine without returning anything. Used when an expedition restarts.</summary>
        public void Clear()
        {
            if (State == WaterProcessorState.Idle && PendingMilliliters == 0 && _processingChunks == 0)
            {
                return;
            }

            _processingChunks = 0;
            _startedAtDays = 0d;
            _readyAtDays = 0d;
            PendingMilliliters = 0;
            State = WaterProcessorState.Idle;
            Changed?.Invoke();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
