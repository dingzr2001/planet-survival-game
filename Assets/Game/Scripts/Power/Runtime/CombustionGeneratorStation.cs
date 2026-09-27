using System.Collections.Generic;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.UI.Power;
using UnityEngine;

namespace PlanetSurvival.Power.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CombustionGeneratorStation : MonoBehaviour, IInteractable
    {
        private const float PullItemsPerSecond = 2f;
        private BuildSite _site;
        private ItemTransferSystem _transferSystem;
        private CombustionGeneratorView _view;
        private float _fuelAllowance;
        private float _oxygenAllowance;
        private readonly List<ItemDefinition> _outputItems = new();
        private readonly Dictionary<string, float> _outputAllowances = new();

        public string Prompt => "use combustion generator";

        public void Bind(BuildSite site, ItemTransferSystem transferSystem, CombustionGeneratorView view)
        {
            _site = site;
            _transferSystem = transferSystem;
            _view = view;
            if (_site?.CombustionGenerator == null || _view == null)
            {
                Debug.LogError($"{nameof(CombustionGeneratorStation)} requires a generator and panel.", this);
                enabled = false;
            }
            if (_site?.CombustionGenerator == null) return;
            foreach (var recipe in _site.CombustionGenerator.Definition.Fuels)
            {
                if (recipe.PrimaryByproduct != null && !_outputItems.Contains(recipe.PrimaryByproduct))
                    _outputItems.Add(recipe.PrimaryByproduct);
                if (recipe.SecondaryByproduct != null && !_outputItems.Contains(recipe.SecondaryByproduct))
                    _outputItems.Add(recipe.SecondaryByproduct);
            }
        }

        public bool CanInteract(in InteractionContext context) =>
            isActiveAndEnabled && _site?.State == BuildState.Completed &&
            _view != null && context.Actor != null && context.Inventory != null;

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(context)) _view.Open(_site, _transferSystem, context.Inventory, context.Actor);
        }

        private void Update()
        {
            if (_site?.State != BuildState.Completed || Time.timeScale <= 0f) return;
            CombustionGenerator generator = _site.CombustionGenerator;
            if (_transferSystem != null)
            {
                Pull(generator.FuelInputPostId, false, ref _fuelAllowance);
                Pull(generator.OxygenInputPostId, true, ref _oxygenAllowance);
            }
            generator.Advance(Time.deltaTime);
            if (_transferSystem != null)
                foreach (ItemDefinition item in _outputItems) Push(item);
        }

        private void Pull(string endpointId, bool oxygen, ref float allowance)
        {
            ItemTransferPostStation station = _transferSystem.GetAdjacentPost(_site, endpointId);
            if (station?.Post == null) return;
            ItemDefinition item = station.Post.BufferedItem;
            if (item == null) return;
            CombustionGenerator generator = _site.CombustionGenerator;
            bool isOxygen = item.ItemId == generator.Definition.OxygenItem.ItemId;
            if (isOxygen != oxygen) return;
            if (!oxygen && generator.FuelInputItemId.Length > 0 &&
                generator.FuelInputItemId != item.ItemId) return;
            allowance = Mathf.Min(PullItemsPerSecond, allowance + PullItemsPerSecond * Time.deltaTime);
            int accepted = generator.AcceptableInputItems(item, Mathf.FloorToInt(allowance));
            if (accepted <= 0) return;
            int extracted = station.Post.Extract(accepted);
            int inserted = generator.InsertInputItems(item, extracted);
            if (inserted != extracted)
            {
                station.Post.Insert(item, extracted - inserted);
                Debug.LogError($"Generator '{name}' could not accept {extracted - inserted} item(s) from transfer post.", this);
            }
            allowance -= extracted;
        }

        private void Push(ItemDefinition item)
        {
            CombustionGenerator generator = _site.CombustionGenerator;
            ItemTransferPostStation destination = _transferSystem.GetAdjacentPost(
                _site, generator.GetOutputPostId(item));
            if (destination?.Post == null || destination.Post.InputEndpointId.Length > 0) return;
            ItemStack stack = null;
            foreach (ItemStack candidate in generator.Byproducts.Stacks)
            {
                if (candidate.Definition.ItemId != item.ItemId) continue;
                stack = candidate;
                break;
            }
            if (stack == null) return;
            float allowance = Mathf.Min(PullItemsPerSecond,
                _outputAllowances.GetValueOrDefault(item.ItemId) + PullItemsPerSecond * Time.deltaTime);
            int accepted = destination.Post.AcceptableQuantity(item,
                Mathf.Min(stack.Quantity, Mathf.FloorToInt(allowance)));
            if (accepted > 0 && InventoryTransfer.Transfer(generator.Byproducts,
                    destination.Post.Buffer, stack.StackId, accepted).Succeeded)
                allowance -= accepted;
            _outputAllowances[item.ItemId] = allowance;
        }
    }
}
