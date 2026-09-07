using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Water.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LandingPodWaterDispenser : MonoBehaviour, IInteractable
    {
        private LiquidContainer _waterSupply;
        private LiquidContainer _bottle;
        private WaterRefillView _refillView;

        public string Prompt => "use the water dispenser";

        public void Bind(LiquidContainer waterSupply, LiquidContainer bottle, WaterRefillView refillView)
        {
            _waterSupply = waterSupply;
            _bottle = bottle;
            _refillView = refillView;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Actor != null && _waterSupply != null && _bottle != null && _refillView != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(context))
            {
                _refillView.Open(
                    _waterSupply,
                    _bottle,
                    context.Actor.GetComponent<PlanarPlayerMotor>(),
                    context.Actor.GetComponent<PlayerInteractor>());
            }
        }
    }
}
