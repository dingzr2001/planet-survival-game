using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Cooking.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Cooking;
using UnityEngine;

namespace PlanetSurvival.Cooking.Runtime
{
    /// <summary>
    /// The interactable half of a cooking spot: it advances the station's timer while its scene is
    /// loaded and opens the cooking panel when the player presses E next to it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CookingStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private CookingStationDefinition _definition;

        private CookingProcess _process;
        private CookingView _view;

        public CookingStationDefinition Definition => _definition;
        public CookingProcess Process => _process;

        public string Prompt
        {
            get
            {
                if (_definition == null)
                {
                    return "use cooking station";
                }

                string stationName = _definition.DisplayName.ToLowerInvariant();
                if (_process == null)
                {
                    return $"use the {stationName}";
                }

                return _process.State switch
                {
                    CookingState.Cooking =>
                        $"check the {stationName} ({DishName()} · {_process.RemainingSeconds:0}s)",
                    CookingState.Ready => $"collect {DishName()} from the {stationName}",
                    _ => $"cook at the {stationName}"
                };
            }
        }

        public void Bind(CookingStationBinding binding)
        {
            _definition = binding.Definition;
            _process = binding.Process;
            _view = binding.View;
            if (_definition != null && !_definition.IsValid(out string error))
            {
                Debug.LogError($"Cooking station '{name}' has an invalid definition: {error}", this);
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Actor != null && context.Inventory != null &&
                   _definition != null && _process != null && _view != null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            _view.Open(
                _definition,
                _process,
                context.Inventory,
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>());
        }

        private void Update()
        {
            _process?.Advance(Time.deltaTime);
        }

        private string DishName()
        {
            if (_process.ActiveRecipe == null)
            {
                return "the dish";
            }

            return _process.BatchCount > 1
                ? $"{_process.ActiveRecipe.DisplayName} ×{_process.BatchCount}"
                : _process.ActiveRecipe.DisplayName;
        }
    }
}
