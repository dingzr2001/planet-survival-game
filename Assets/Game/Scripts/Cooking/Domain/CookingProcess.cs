using System;
using System.Collections.Generic;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Inventory.Domain;

namespace PlanetSurvival.Cooking.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// One cooking slot of one station. Unlike the instant <see cref="Crafting.Application.CraftingService"/>,
    /// the ingredients leave the backpack the moment cooking starts and the dish stays inside the station
    /// until it is collected, so a full backpack can never destroy a finished meal.
    /// This is plain state: the station component drives <see cref="Advance"/>, and the session owns the
    /// instance so a pot keeps cooking across deck changes.
    /// </summary>
    public sealed class CookingProcess
    {
        private readonly string _stationId;
        private CraftingRecipe _recipe;
        private float _remainingSeconds;
        private float _totalSeconds;
        private int _batchCount;

        public CookingProcess(string stationId)
        {
            if (string.IsNullOrWhiteSpace(stationId))
            {
                throw new ArgumentException("A cooking process needs a station ID.", nameof(stationId));
            }

            _stationId = stationId;
        }

        public string StationId => _stationId;
        public CookingState State { get; private set; } = CookingState.Idle;
        public CraftingRecipe ActiveRecipe => _recipe;

        /// <summary>How many times the active recipe runs in the current batch.</summary>
        public int BatchCount => _batchCount;
        public float RemainingSeconds => _remainingSeconds;
        public float TotalSeconds => _totalSeconds;

        public float Progress => _totalSeconds <= 0f
            ? (State == CookingState.Idle ? 0f : 1f)
            : Math.Clamp(1f - (_remainingSeconds / _totalSeconds), 0f, 1f);

        /// <summary>Raised whenever the state, the recipe, or the readiness of the dish changes.</summary>
        public event Action Changed;

        /// <summary>
        /// Consumes the ingredients for the whole batch and starts the timer. A batch runs the recipe
        /// <paramref name="batchCount"/> times: it takes that many times as long and yields that many
        /// times the output, all collected in one go.
        /// </summary>
        public CraftingResult Start(
            CraftingRecipe recipe, InventoryModel inventory, ICraftingConditionProvider conditions,
            int batchCount = 1)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (State != CookingState.Idle)
            {
                return CraftingResult.Fail(
                    CraftingFailure.StationBusy,
                    State == CookingState.Ready
                        ? "Collect the finished dish before cooking again."
                        : "The station is already cooking.");
            }

            CraftingResult validation = Validate(recipe, inventory, conditions, batchCount);
            if (!validation.Succeeded)
            {
                return validation;
            }

            InventoryOperationResult consumed = inventory.ApplyTransaction(
                CraftingItemAmount.ToInventoryAmounts(recipe.Inputs, batchCount), null);
            if (!consumed.Succeeded)
            {
                return ToCraftingResult(consumed);
            }

            _recipe = recipe;
            _batchCount = batchCount;
            _totalSeconds = recipe.DurationSeconds * batchCount;
            _remainingSeconds = _totalSeconds;
            State = _totalSeconds <= 0f ? CookingState.Ready : CookingState.Cooking;
            Changed?.Invoke();
            return CraftingResult.Success();
        }

        /// <summary>How many times the backpack can run this recipe right now. Zero when an input is missing.</summary>
        public static int MaxBatches(CraftingRecipe recipe, InventoryModel inventory)
        {
            if (recipe == null || inventory == null || recipe.Inputs.Count == 0)
            {
                return 0;
            }

            int batches = int.MaxValue;
            IReadOnlyList<CraftingItemAmount> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                CraftingItemAmount input = inputs[i];
                if (input.Item == null || input.Quantity <= 0)
                {
                    return 0;
                }

                batches = Math.Min(batches, inventory.GetQuantity(input.Item.ItemId) / input.Quantity);
            }

            return batches;
        }

        /// <summary>Checks a recipe without touching the inventory, so the UI can grey out what is missing.</summary>
        public CraftingResult Validate(
            CraftingRecipe recipe, InventoryModel inventory, ICraftingConditionProvider conditions,
            int batchCount = 1)
        {
            if (batchCount < 1)
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, "A batch must cook at least one item.");
            }

            if (recipe == null)
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, "Cooking recipe is missing.");
            }

            if (!recipe.IsValid(out string recipeError))
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, recipeError);
            }

            if (conditions == null)
            {
                return CraftingResult.Fail(CraftingFailure.MissingCondition, "Cooking conditions are unavailable.");
            }

            for (int i = 0; i < recipe.RequiredConditionIds.Count; i++)
            {
                string conditionId = recipe.RequiredConditionIds[i];
                if (!conditions.IsConditionMet(conditionId))
                {
                    return CraftingResult.Fail(
                        CraftingFailure.MissingCondition,
                        $"'{recipe.DisplayName}' cannot be prepared at station '{_stationId}'.");
                }
            }

            if (inventory == null)
            {
                return CraftingResult.Fail(CraftingFailure.MissingIngredients, "No backpack to draw ingredients from.");
            }

            IReadOnlyList<CraftingItemAmount> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                CraftingItemAmount input = inputs[i];
                if (inventory.GetQuantity(input.Item.ItemId) < input.Quantity * batchCount)
                {
                    return CraftingResult.Fail(
                        CraftingFailure.MissingIngredients,
                        $"Not enough '{input.Item.DisplayName}' for {batchCount} × '{recipe.DisplayName}'.");
                }
            }

            return CraftingResult.Success();
        }

        public void Advance(float elapsedSeconds)
        {
            if (State != CookingState.Cooking || elapsedSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds -= elapsedSeconds;
            if (_remainingSeconds > 0f)
            {
                Changed?.Invoke();
                return;
            }

            _remainingSeconds = 0f;
            State = CookingState.Ready;
            Changed?.Invoke();
        }

        /// <summary>Moves the finished dish into the backpack. A full backpack leaves it in the station.</summary>
        public CraftingResult Collect(InventoryModel inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (State != CookingState.Ready)
            {
                return CraftingResult.Fail(CraftingFailure.NothingReady, "No dish is ready to collect.");
            }

            InventoryOperationResult stored = inventory.ApplyTransaction(
                null, CraftingItemAmount.ToInventoryAmounts(_recipe.Outputs, _batchCount));
            if (!stored.Succeeded)
            {
                return ToCraftingResult(stored);
            }

            Clear();
            return CraftingResult.Success();
        }

        /// <summary>Aborts a running craft and returns the ingredients, so a misclick costs only time.</summary>
        public CraftingResult Cancel(InventoryModel inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (State != CookingState.Cooking)
            {
                return CraftingResult.Fail(
                    CraftingFailure.NothingReady,
                    State == CookingState.Ready
                        ? "The dish is already cooked; collect it instead."
                        : "The station is not cooking.");
            }

            InventoryOperationResult refunded = inventory.ApplyTransaction(
                null, CraftingItemAmount.ToInventoryAmounts(_recipe.Inputs, _batchCount));
            if (!refunded.Succeeded)
            {
                return ToCraftingResult(refunded);
            }

            Clear();
            return CraftingResult.Success();
        }

        /// <summary>Empties the station without refunding anything. Used when a session restarts.</summary>
        public void Clear()
        {
            if (State == CookingState.Idle && _recipe == null)
            {
                return;
            }

            _recipe = null;
            _batchCount = 0;
            _remainingSeconds = 0f;
            _totalSeconds = 0f;
            State = CookingState.Idle;
            Changed?.Invoke();
        }

        private static CraftingResult ToCraftingResult(InventoryOperationResult result)
        {
            CraftingFailure failure = result.Failure == InventoryFailure.InsufficientQuantity
                ? CraftingFailure.MissingIngredients
                : CraftingFailure.InventoryFull;
            return CraftingResult.Fail(failure, result.Message);
        }
    }
}
