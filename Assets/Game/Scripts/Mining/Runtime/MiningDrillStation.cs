using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using UnityEngine;

namespace PlanetSurvival.Mining.Runtime
{
    /// <summary>Scene bridge that advances a session-owned drill and opens its interaction panel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class MiningDrillStation : MonoBehaviour, IInteractable
    {
        private MiningDrill _drill;
        private PlanetSurvival.UI.Mining.MiningDrillView _view;

        public MiningDrill Drill => _drill;

        public string Prompt
        {
            get
            {
                if (_drill == null)
                {
                    return "inspect mining drill";
                }

                string state = _drill.State switch
                {
                    MiningDrillState.StorageFull => "storage full",
                    MiningDrillState.Producing => "running",
                    _ => "needs power or petroleum"
                };
                return $"use iron mining drill ({_drill.StoredOre}/{_drill.Definition.OreCapacity} ore · {state})";
            }
        }

        public void Bind(MiningDrillBinding binding)
        {
            _drill = binding.Drill;
            _view = binding.View;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return isActiveAndEnabled && context.Actor != null && context.Inventory != null &&
                   _drill != null && _view != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _drill,
                context.Inventory,
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }

        private void Update()
        {
            _drill?.Advance(Time.deltaTime);
        }
    }
}
