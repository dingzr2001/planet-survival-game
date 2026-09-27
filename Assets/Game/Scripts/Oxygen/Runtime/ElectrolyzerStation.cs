using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;

namespace PlanetSurvival.Oxygen.Runtime
{
    /// <summary>Scene bridge that advances a session-owned electrolyzer and opens its operations panel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ElectrolyzerStation : MonoBehaviour, IInteractable
    {
        private Electrolyzer _electrolyzer;
        private UI.Oxygen.ElectrolyzerView _view;
        private Sprite _panelIcon;

        /// <summary>Pipe and power builders connect to this machine through its input/output interfaces.</summary>
        public Electrolyzer Electrolyzer => _electrolyzer;

        public string Prompt => _electrolyzer == null
            ? "use electrolyzer"
            : $"use electrolyzer ({_electrolyzer.StoredOxygenLiters:0} L O₂ · {StateLabel(_electrolyzer.State)})";

        /// <param name="panelIcon">Artwork the operations panel titles itself with; optional.</param>
        public void Bind(ElectrolyzerBinding binding, Sprite panelIcon = null)
        {
            _electrolyzer = binding.Electrolyzer;
            _view = binding.View;
            _panelIcon = panelIcon;
            if (!binding.IsComplete)
            {
                Debug.LogError($"Electrolyzer '{name}' was bound with incomplete configuration.", this);
                enabled = false;
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            return isActiveAndEnabled && context.Actor != null && context.Inventory != null &&
                   _electrolyzer != null && _view != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _electrolyzer,
                context.Inventory,
                context.Actor.GetComponent<PlayerWaterBottle>(),
                context.Actor.GetComponent<PlayerSpaceSuit>(),
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>(),
                _panelIcon);
        }

        private void Update()
        {
            _electrolyzer?.Advance(Time.deltaTime);
        }

        private static string StateLabel(ElectrolyzerState state) => state switch
        {
            ElectrolyzerState.NeedsWater => "needs water",
            ElectrolyzerState.NeedsPower => "needs power",
            ElectrolyzerState.HydrogenStorageFull => "vent H₂",
            ElectrolyzerState.OxygenStorageFull => "storage full",
            _ => "running"
        };
    }
}
