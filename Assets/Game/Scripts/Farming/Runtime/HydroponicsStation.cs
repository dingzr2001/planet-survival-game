using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Farming;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Farming.Runtime
{
    /// <summary>
    /// The interactable half of the hydroponics rack. It holds no growth state of its own — the trays
    /// live in the session and ripen on expedition time — so it only reports what the rack is doing and
    /// opens the planting panel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HydroponicsStation : MonoBehaviour, IInteractable
    {
        private HydroponicsRack _rack;
        private CropDefinition _crop;
        private LiquidContainer _waterSupply;
        private HydroponicsView _view;
        private GameClock _clock;

        public HydroponicsRack Rack => _rack;

        public string Prompt
        {
            get
            {
                if (_rack == null || _clock == null)
                {
                    return "use the hydroponics rack";
                }

                int ripe = _rack.RipeCount(_clock.ElapsedDays);
                if (ripe > 0)
                {
                    return $"harvest the hydroponics rack ({ripe} ready)";
                }

                return _rack.FirstEmptySlot() != null
                    ? "plant in the hydroponics rack"
                    : "check the hydroponics rack";
            }
        }

        public void Bind(HydroponicsBinding binding)
        {
            _rack = binding.Rack;
            _crop = binding.Crop;
            _waterSupply = binding.WaterSupply;
            _view = binding.View;
            _clock = binding.Clock;
            if (!binding.IsComplete)
            {
                Debug.LogError($"Hydroponics rack '{name}' was bound with incomplete configuration.", this);
                enabled = false;
                return;
            }

            if (!_crop.IsValid(out string cropError))
            {
                Debug.LogError($"Hydroponics rack '{name}' grows an invalid crop: {cropError}", this);
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Actor != null && context.Inventory != null && _rack != null && _crop != null
                   && _waterSupply != null && _view != null && _clock != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _rack,
                _crop,
                _waterSupply,
                _clock,
                context.Inventory,
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }
    }
}
