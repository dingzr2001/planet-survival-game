using System;
using System.Collections.Generic;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Power.Definitions;
using UnityEngine;

namespace PlanetSurvival.Power.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>Session-owned fuel, oxygen and exhaust buffers with live power output.</summary>
    public sealed class CombustionGenerator : IItemInput, IItemOutput
    {
        private const float Epsilon = .0001f;
        private const float OutputItemsPerSecond = 2f;
        private readonly InventoryModel _byproducts = new(int.MaxValue);
        private readonly Dictionary<string, string> _outputPostIds = new(StringComparer.Ordinal);
        private float _outputAllowance;

        public CombustionGenerator(CombustionGeneratorDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _byproducts.Changed += () => Changed?.Invoke();
        }

        public CombustionGeneratorDefinition Definition { get; }
        public ItemDefinition FuelItem { get; private set; }
        public int FuelQuantity { get; private set; }
        public int OxygenQuantity { get; private set; }
        public float BurnProgressSeconds { get; private set; }
        public float CurrentPowerWatts
        {
            get
            {
                if (FuelItem == null || !Definition.TryGetRecipe(FuelItem, out CombustionFuelRecipe recipe) ||
                    !CanBurn(recipe)) return 0f;
                return recipe.ElectricityPerBurn / recipe.BurnSeconds;
            }
        }
        public bool IsBurning => CurrentPowerWatts > 0f;
        public string FuelInputPostId => _fuelInputPostId;
        public string FuelInputItemId { get; private set; } = string.Empty;
        public string OxygenInputPostId => _oxygenInputPostId;
        public InventoryModel Byproducts => _byproducts;
        public ItemDefinition OutputItem => _byproducts.Stacks.Count > 0 ? _byproducts.Stacks[0].Definition : null;
        public int ByproductQuantity
        {
            get
            {
                int quantity = 0;
                foreach (ItemStack stack in _byproducts.Stacks) quantity += stack.Quantity;
                return quantity;
            }
        }
        public event Action Changed;

        public void ConfigureFuelInput(string postId) => ConfigureFuelInput(postId, null);

        public void ConfigureFuelInput(string postId, ItemDefinition item)
        {
            string itemId = item != null && Definition.TryGetRecipe(item, out _) ? item.ItemId : string.Empty;
            bool changed = _fuelInputPostId != (postId ?? string.Empty) || FuelInputItemId != itemId;
            _fuelInputPostId = postId ?? string.Empty;
            FuelInputItemId = itemId;
            if (changed) Changed?.Invoke();
        }

        public void ConfigureOxygenInput(string postId) => SetInput(ref _oxygenInputPostId, postId);

        public string GetOutputPostId(ItemDefinition item) => item != null &&
            _outputPostIds.TryGetValue(item.ItemId, out string postId) ? postId : string.Empty;

        public void ConfigureOutputPost(ItemDefinition item, string postId)
        {
            if (item == null || !IsByproduct(item)) return;
            string normalized = postId ?? string.Empty;
            if (GetOutputPostId(item) == normalized) return;
            if (normalized.Length == 0) _outputPostIds.Remove(item.ItemId);
            else _outputPostIds[item.ItemId] = normalized;
            Changed?.Invoke();
        }

        // Backing fields keep the public route identifiers read-only to other systems.
        private string _fuelInputPostId = string.Empty;
        private string _oxygenInputPostId = string.Empty;

        private void SetInput(ref string field, string postId)
        {
            string normalized = postId ?? string.Empty;
            if (field == normalized) return;
            field = normalized;
            Changed?.Invoke();
        }

        private bool IsByproduct(ItemDefinition item)
        {
            foreach (CombustionFuelRecipe recipe in Definition.Fuels)
                if (recipe.PrimaryByproduct == item || recipe.SecondaryByproduct == item) return true;
            return false;
        }

        public int AcceptableInputItems(ItemDefinition item, int maximumQuantity)
        {
            if (item == null || maximumQuantity <= 0) return 0;
            if (item.ItemId == Definition.OxygenItem.ItemId)
                return Math.Min(maximumQuantity, Definition.OxygenCapacity - OxygenQuantity);
            if (!Definition.TryGetRecipe(item, out _) ||
                (FuelItem != null && FuelItem.ItemId != item.ItemId)) return 0;
            return Math.Min(maximumQuantity, Definition.FuelCapacity - FuelQuantity);
        }

        public int InsertInputItems(ItemDefinition item, int quantity)
        {
            int accepted = AcceptableInputItems(item, quantity);
            if (accepted <= 0) return 0;
            if (item.ItemId == Definition.OxygenItem.ItemId) OxygenQuantity += accepted;
            else { FuelItem = item; FuelQuantity += accepted; }
            Changed?.Invoke();
            return accepted;
        }

        public int LoadFromInventory(ItemDefinition item, int requested, InventoryModel inventory)
        {
            if (inventory == null || item == null) return 0;
            int quantity = Math.Min(AcceptableInputItems(item, requested), inventory.GetQuantity(item.ItemId));
            if (quantity <= 0 || !inventory.ApplyTransaction(
                    new[] { new InventoryItemAmount(item, quantity) }, null).Succeeded) return 0;
            return InsertInputItems(item, quantity);
        }

        public int CollectByproduct(ItemDefinition item, int requested, InventoryModel inventory)
        {
            if (inventory == null || item == null || requested <= 0) return 0;
            int quantity = Math.Min(requested, _byproducts.GetQuantity(item.ItemId));
            while (quantity > 0 && !inventory.CanAdd(item, quantity).Succeeded) quantity--;
            if (quantity <= 0) return 0;
            if (!_byproducts.ApplyTransaction(
                    new[] { new InventoryItemAmount(item, quantity) }, null).Succeeded) return 0;
            if (!inventory.Add(item, quantity).Succeeded)
            {
                _byproducts.Add(item, quantity);
                return 0;
            }
            return quantity;
        }

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || FuelItem == null || !Definition.TryGetRecipe(FuelItem, out CombustionFuelRecipe recipe)) return;
            while (elapsedSeconds > Epsilon && CanBurn(recipe))
            {
                float remaining = recipe.BurnSeconds - BurnProgressSeconds;
                float advanced = Mathf.Min(elapsedSeconds, remaining);
                BurnProgressSeconds += advanced;
                elapsedSeconds -= advanced;
                if (BurnProgressSeconds + Epsilon < recipe.BurnSeconds) break;
                FuelQuantity -= recipe.FuelItems;
                OxygenQuantity -= recipe.OxygenItems;
                _byproducts.Add(recipe.PrimaryByproduct, recipe.PrimaryQuantity);
                if (recipe.SecondaryQuantity > 0)
                    _byproducts.Add(recipe.SecondaryByproduct, recipe.SecondaryQuantity);
                BurnProgressSeconds = 0f;
                if (FuelQuantity == 0) FuelItem = null;
                Changed?.Invoke();
            }
        }

        public int Extract(int maximumQuantity, float elapsedSeconds)
        {
            if (maximumQuantity <= 0 || elapsedSeconds <= 0f || OutputItem == null) return 0;
            _outputAllowance = Mathf.Min(OutputItemsPerSecond,
                _outputAllowance + OutputItemsPerSecond * elapsedSeconds);
            int quantity = Math.Min(maximumQuantity,
                Math.Min(Mathf.FloorToInt(_outputAllowance + Epsilon), _byproducts.Stacks[0].Quantity));
            if (quantity <= 0) return 0;
            if (!_byproducts.Remove(_byproducts.Stacks[0].StackId, quantity).Succeeded) return 0;
            _outputAllowance -= quantity;
            return quantity;
        }

        private bool CanBurn(CombustionFuelRecipe recipe)
        {
            return FuelQuantity >= recipe.FuelItems && OxygenQuantity >= recipe.OxygenItems &&
                   ByproductQuantity + recipe.PrimaryQuantity + recipe.SecondaryQuantity <= Definition.ByproductCapacity;
        }

    }
}
