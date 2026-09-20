using System;
using System.Collections.Generic;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.World.Ground;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Building.Application
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// The rules of building: what the backpack can afford, which cells are still free, and the timers of
    /// every site that has been placed. It owns no scene objects — the placement controller listens to the
    /// events and keeps the world in sync — so the whole flow stays testable without a scene.
    /// </summary>
    public sealed class BuildingService
    {
        private readonly InventoryModel _inventory;
        private readonly BuildGrid _grid;
        private readonly TerrainTileMap _terrain;
        private readonly List<BuildSite> _sites = new();

        public BuildingService(InventoryModel inventory, BuildGrid grid, TerrainTileMap terrain = null)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _terrain = terrain;
        }

        public BuildGrid Grid => _grid;
        public IReadOnlyList<BuildSite> Sites => _sites;

        public event Action<BuildSite> SitePlaced;
        public event Action<BuildSite> SiteCompleted;
        public event Action<BuildSite> SiteRemoved;

        /// <summary>True when the backpack currently holds the full construction cost.</summary>
        public bool CanAfford(BuildableDefinition buildable)
        {
            if (buildable == null)
            {
                return false;
            }

            IReadOnlyList<CraftingItemAmount> cost = buildable.Cost;
            for (int i = 0; i < cost.Count; i++)
            {
                if (cost[i].Item == null || _inventory.GetQuantity(cost[i].Item.ItemId) < cost[i].Quantity)
                {
                    return false;
                }
            }

            IReadOnlyList<TaggedBuildingMaterialAmount> taggedCost = buildable.TaggedCost;
            for (int i = 0; i < taggedCost.Count; i++)
            {
                if (CountTaggedItems(taggedCost[i].MaterialTag) < taggedCost[i].Quantity)
                {
                    return false;
                }
            }

            return cost.Count > 0 || taggedCost.Count > 0;
        }

        /// <summary>Checks a spot without changing anything, so the preview can be tinted before the click.</summary>
        public BuildResult CanPlace(BuildableDefinition buildable, in BuildFootprint footprint)
        {
            if (buildable == null)
            {
                return BuildResult.Fail(BuildFailure.InvalidBuildable, "No structure is selected.");
            }

            if (!buildable.IsValid(out string error))
            {
                return BuildResult.Fail(BuildFailure.InvalidBuildable, error);
            }

            if (footprint.Size != buildable.Footprint)
            {
                return BuildResult.Fail(
                    BuildFailure.InvalidBuildable,
                    $"'{buildable.DisplayName}' occupies {buildable.Footprint.x}×{buildable.Footprint.y} cells.");
            }

            if (!_grid.IsFree(footprint))
            {
                return BuildResult.Fail(BuildFailure.Blocked, "Something already stands here.");
            }

            string requiredTerrainId = buildable.MiningDrill?.RequiredTerrainId;
            if (!string.IsNullOrWhiteSpace(requiredTerrainId) && !IsFootprintOnTerrain(footprint, requiredTerrainId))
            {
                return BuildResult.Fail(
                    BuildFailure.WrongTerrain,
                    $"'{buildable.DisplayName}' can only be placed on {buildable.MiningDrill.RequiredTerrainDisplayName} terrain.");
            }

            if (!CanAfford(buildable))
            {
                return BuildResult.Fail(
                    BuildFailure.MissingResources,
                    $"Your backpack lacks the materials for '{buildable.DisplayName}'.");
            }

            return BuildResult.Success();
        }

        private bool IsFootprintOnTerrain(in BuildFootprint footprint, string terrainId)
        {
            if (_terrain == null)
            {
                return false;
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                Vector3 center = _grid.CellCenter(footprint.CellAt(i));
                TerrainSurfaceDefinition surface = _terrain.GetSurface(_terrain.TileAt(center));
                if (surface == null || !string.Equals(surface.TerrainId, terrainId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Pays the materials, claims the cells, and starts the construction timer. The cost leaves the
        /// backpack immediately, so a site can never be paid for twice.
        /// </summary>
        public BuildResult TryPlace(BuildableDefinition buildable, in BuildFootprint footprint, out BuildSite site)
        {
            site = null;
            BuildResult validation = CanPlace(buildable, footprint);
            if (!validation.Succeeded)
            {
                return validation;
            }

            List<InventoryItemAmount> paidMaterials = ResolvePayment(buildable);
            InventoryOperationResult paid = _inventory.ApplyTransaction(paidMaterials, null);
            if (!paid.Succeeded)
            {
                return BuildResult.Fail(BuildFailure.MissingResources, paid.Message);
            }

            var placed = new BuildSite(Guid.NewGuid().ToString("N"), buildable, footprint, paidMaterials);
            if (!_grid.TryOccupy(footprint, placed))
            {
                // Unreachable while CanPlace holds, but refunding keeps a future caller from losing materials.
                _inventory.ApplyTransaction(null, paidMaterials);
                return BuildResult.Fail(BuildFailure.Blocked, "Something already stands here.");
            }

            _sites.Add(placed);
            site = placed;
            SitePlaced?.Invoke(placed);
            if (placed.State == BuildState.Completed)
            {
                SiteCompleted?.Invoke(placed);
            }
            else
            {
                placed.Completed += () => SiteCompleted?.Invoke(placed);
            }

            return BuildResult.Success();
        }

        /// <summary>Aborts an unfinished site and returns its materials, so a misplaced click costs only time.</summary>
        public BuildResult Cancel(BuildSite site)
        {
            if (site == null || !_sites.Contains(site))
            {
                return BuildResult.Fail(BuildFailure.InvalidBuildable, "That construction site is gone.");
            }

            if (site.State != BuildState.UnderConstruction)
            {
                return BuildResult.Fail(
                    BuildFailure.NotUnderConstruction,
                    $"'{site.Definition.DisplayName}' is already finished.");
            }

            InventoryOperationResult refunded = _inventory.ApplyTransaction(
                null, site.PaidMaterials);
            if (!refunded.Succeeded)
            {
                return BuildResult.Fail(BuildFailure.InventoryFull, refunded.Message);
            }

            Remove(site);
            return BuildResult.Success();
        }

        /// <summary>Drops a site from the world without refunding anything.</summary>
        public void Remove(BuildSite site)
        {
            if (site == null || !_sites.Remove(site))
            {
                return;
            }

            _grid.Release(site);
            SiteRemoved?.Invoke(site);
        }

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                return;
            }

            // Iterated backwards because a completion handler may remove or replace its own site.
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                if (i < _sites.Count)
                {
                    _sites[i].Advance(elapsedSeconds);
                }
            }
        }

        /// <summary>Wipes every player-built site. Environment occupants registered on the grid remain.</summary>
        public void Clear()
        {
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                BuildSite site = _sites[i];
                _sites.RemoveAt(i);
                _grid.Release(site);
                SiteRemoved?.Invoke(site);
            }
        }

        public int CountTaggedItems(ItemMaterialTag materialTag)
        {
            int quantity = 0;
            IReadOnlyList<ItemStack> stacks = _inventory.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].Definition.HasMaterialTag(materialTag))
                {
                    quantity += stacks[i].Quantity;
                }
            }

            return quantity;
        }

        private List<InventoryItemAmount> ResolvePayment(BuildableDefinition buildable)
        {
            List<InventoryItemAmount> payment = CraftingItemAmount.ToInventoryAmounts(buildable.Cost);
            IReadOnlyList<TaggedBuildingMaterialAmount> taggedCost = buildable.TaggedCost;
            for (int requirementIndex = 0; requirementIndex < taggedCost.Count; requirementIndex++)
            {
                TaggedBuildingMaterialAmount requirement = taggedCost[requirementIndex];
                int remaining = requirement.Quantity;
                IReadOnlyList<ItemStack> stacks = _inventory.Stacks;
                for (int stackIndex = 0; stackIndex < stacks.Count && remaining > 0; stackIndex++)
                {
                    ItemStack stack = stacks[stackIndex];
                    if (!stack.Definition.HasMaterialTag(requirement.MaterialTag)) continue;
                    int taken = Math.Min(remaining, stack.Quantity);
                    AddOrMerge(payment, stack.Definition, taken);
                    remaining -= taken;
                }
            }

            return payment;
        }

        private static void AddOrMerge(List<InventoryItemAmount> amounts, ItemDefinition item, int quantity)
        {
            for (int i = 0; i < amounts.Count; i++)
            {
                if (amounts[i].Definition.ItemId != item.ItemId) continue;
                amounts[i] = new InventoryItemAmount(item, amounts[i].Quantity + quantity);
                return;
            }

            amounts.Add(new InventoryItemAmount(item, quantity));
        }
    }
}
