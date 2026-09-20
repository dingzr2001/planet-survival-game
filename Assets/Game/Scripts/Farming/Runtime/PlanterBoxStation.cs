using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;

namespace PlanetSurvival.Farming.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlanterBoxStation : MonoBehaviour, IInteractable
    {
        private PlanterBox _planterBox;
        private UI.Farming.PlanterBoxView _view;

        /// <summary>Pipe builders connect to this system through its input/output interfaces.</summary>
        public PlanterBox PlanterBox => _planterBox;

        public string Prompt => _planterBox == null
            ? "use planter box"
            : $"use planter box · {StateLabel(_planterBox.State)}";

        public void Bind(PlanterBoxBinding binding)
        {
            _planterBox = binding.PlanterBox;
            _view = binding.View;
            if (!binding.IsComplete)
            {
                Debug.LogError($"Planter box '{name}' was bound with incomplete configuration.", this);
                enabled = false;
            }
        }

        public bool CanInteract(in InteractionContext context) =>
            context.Actor != null && context.Inventory != null && _planterBox != null && _view != null;

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            _view.Open(_planterBox, context.Inventory,
                context.Actor.GetComponent<PlayerWaterBottle>(),
                context.Actor.GetComponent<PlayerSpaceSuit>(),
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }

        private static string StateLabel(PlanterBoxState state) => state switch
        {
            PlanterBoxState.NeedsWater => "needs water",
            PlanterBoxState.NeedsCarbonDioxide => "needs CO₂",
            PlanterBoxState.OxygenStorageFull => "oxygen full",
            _ => "producing oxygen"
        };
    }
}
