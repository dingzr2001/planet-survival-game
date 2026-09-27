using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;
using PlanetSurvival.Core.Time;
using PlanetSurvival.World.Presentation;

namespace PlanetSurvival.Farming.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlanterBoxStation : MonoBehaviour, IInteractable
    {
        private PlanterBox _planterBox;
        private UI.Farming.PlanterBoxView _view;
        private GameClock _clock;
        private WorldSpriteView _worldSprite;
        private Sprite _panelIcon;

        /// <summary>Pipe builders connect to this system through its input/output interfaces.</summary>
        public PlanterBox PlanterBox => _planterBox;

        public string Prompt => _planterBox == null
            ? "use planter box"
            : $"use planter box · {StateLabel(_planterBox.State)}";

        /// <param name="panelIcon">Artwork the operations panel titles itself with; optional.</param>
        public void Bind(PlanterBoxBinding binding, Transform body = null, Sprite panelIcon = null)
        {
            _planterBox = binding.PlanterBox;
            _view = binding.View;
            _clock = binding.Clock;
            _panelIcon = panelIcon;
            _worldSprite = body != null ? body.GetComponentInChildren<WorldSpriteView>(true) : null;
            if (!binding.IsComplete)
            {
                Debug.LogError($"Planter box '{name}' was bound with incomplete configuration.", this);
                enabled = false;
            }

            if (_planterBox != null)
            {
                _planterBox.Changed += RefreshPresentation;
                RefreshPresentation();
            }
        }

        public bool CanInteract(in InteractionContext context) =>
            context.Actor != null && context.Inventory != null && _planterBox != null && _view != null;

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            _view.Open(_planterBox, context.Inventory,
                _clock,
                context.Actor.GetComponent<PlayerWaterBottle>(),
                context.Actor.GetComponent<PlayerSpaceSuit>(),
                context.Actor.GetComponent<PlanarPlayerMotor>(),
                context.Actor.GetComponent<PlayerInteractor>(),
                _panelIcon);
        }

        private void OnDestroy()
        {
            if (_planterBox != null) _planterBox.Changed -= RefreshPresentation;
        }

        public void RefreshPresentation()
        {
            if (_worldSprite == null || _planterBox == null) return;
            Sprite sprite = _planterBox.IsPlanted && !_planterBox.IsDead
                ? _planterBox.Crop.GrowthStageSprite(_planterBox.GrowthProgress)
                : _planterBox.Definition.EmptySprite;
            if (sprite != null) _worldSprite.SetStaticSprite(sprite);
        }

        private static string StateLabel(PlanterBoxState state) => state switch
        {
            PlanterBoxState.Empty => "empty",
            PlanterBoxState.NeedsWater => "needs water",
            PlanterBoxState.NeedsCarbonDioxide => "needs CO₂",
            PlanterBoxState.Mature => "ready to harvest",
            PlanterBoxState.Dead => "crop died",
            PlanterBoxState.OxygenStorageFull => "oxygen full",
            _ => "crop growing"
        };
    }
}
