using PlanetSurvival.Core.Time;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Water.Runtime
{
    /// <summary>
    /// The interactable half of the ice processor: it settles a finished batch while its scene is loaded
    /// and opens the processor panel when the player presses E next to it. The batch itself is stamped
    /// against expedition time, so ice also is purified while this scene is unloaded.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WaterProcessorStation : MonoBehaviour, IInteractable
    {
        private WaterProcessor _processor;
        private ItemDefinition _iceItem;
        private LiquidContainer _waterSupply;
        private WaterProcessorView _view;
        private GameClock _clock;

        public WaterProcessor Processor => _processor;

        public string Prompt
        {
            get
            {
                if (_processor == null || _clock == null)
                {
                    return "use the water processor";
                }

                return _processor.State switch
                {
                    WaterProcessorState.Processing =>
                        $"check the water processor ({_processor.RemainingGameHours(_clock.ElapsedDays):0.0}h left)",
                    WaterProcessorState.Ready =>
                        $"drain {_processor.PendingMilliliters / 1000f:0.0} L from the water processor",
                    _ => "load the water processor"
                };
            }
        }

        public void Bind(WaterProcessorBinding binding)
        {
            _processor = binding.Processor;
            _iceItem = binding.IceItem;
            _waterSupply = binding.WaterSupply;
            _view = binding.View;
            _clock = binding.Clock;
            if (!binding.IsComplete)
            {
                Debug.LogError($"Water processor '{name}' was bound with incomplete configuration.", this);
                enabled = false;
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Actor != null && context.Inventory != null && _processor != null
                   && _iceItem != null && _waterSupply != null && _view != null && _clock != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _processor,
                _iceItem,
                _waterSupply,
                _clock,
                context.Inventory,
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }

        private void Update()
        {
            if (_processor != null && _clock != null)
            {
                _processor.Advance(_clock.ElapsedDays);
            }
        }
    }
}
