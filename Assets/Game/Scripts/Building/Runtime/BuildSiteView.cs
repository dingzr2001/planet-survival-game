using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// The scene half of one <see cref="BuildSite"/>: a translucent scaffold that grows while the timer
    /// runs, then becomes the solid structure. Pressing E on an unfinished site tears it down and returns
    /// its materials; a finished building keeps whatever interaction its buildable gives it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildSiteView : MonoBehaviour, IInteractable
    {
        private static readonly Color ScaffoldTint = new(.55f, .85f, 1f, .55f);

        private BuildSite _site;
        private BuildingService _service;
        private Transform _body;
        private SpriteRenderer _patch;
        private BoxCollider _collider;
        private float _cellSize = BuildGrid.DefaultCellSize;

        public BuildSite Site => _site;

        public string Prompt
        {
            get
            {
                if (_site == null)
                {
                    return string.Empty;
                }

                string structure = _site.Definition.DisplayName.ToLowerInvariant();
                return _site.State == BuildState.UnderConstruction
                    ? $"cancel the {structure} ({_site.RemainingSeconds:0}s left)"
                    : string.Empty;
            }
        }

        public void Bind(BuildSite site, BuildingService service, float cellSize,
            WorldVisualSettings visuals, CookingStationBinding cooking = default)
        {
            _site = site;
            _service = service;
            _cellSize = Mathf.Max(.05f, cellSize);
            BuildableDefinition buildable = site.Definition;

            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.size = new Vector3(
                buildable.Footprint.x * _cellSize,
                buildable.WorldHeight,
                buildable.Footprint.y * _cellSize);
            _collider.center = Vector3.up * (_collider.size.y * .5f);

            _body = BuildingVisuals.CreateBody(transform, buildable, _cellSize);
            _patch = BuildingVisuals.CreateFootprintPatch(transform, buildable.Footprint, _cellSize);

            if (cooking.IsComplete)
            {
                // The station only becomes usable once the structure stands, so it starts disabled.
                CookingStation station = gameObject.AddComponent<CookingStation>();
                station.Bind(cooking);
                station.enabled = false;
            }

            Color shadowColor = visuals != null ? visuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(transform,
                new Vector2(buildable.Footprint.x * _cellSize, buildable.Footprint.y * _cellSize * .8f),
                shadowColor);

            _site.Changed += Refresh;
            Refresh();
        }

        public bool CanInteract(in InteractionContext context)
        {
            return _site != null && _service != null && context.Inventory != null &&
                   _site.State == BuildState.UnderConstruction;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            BuildResult result = _service.Cancel(_site);
            if (result.Succeeded)
            {
                context.Inventory.RefreshQuickBarAssignments();
                return;
            }

            Debug.LogWarning($"The {_site.Definition.DisplayName} could not be cancelled: {result.Message}", this);
        }

        private void OnDestroy()
        {
            if (_site != null)
            {
                _site.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            if (_site == null || _body == null)
            {
                return;
            }

            if (_site.State == BuildState.UnderConstruction)
            {
                // The scaffold rises out of the ground as the timer runs, so progress is readable at a glance.
                _body.localScale = new Vector3(1f, Mathf.Lerp(.15f, 1f, _site.Progress), 1f);
                BuildingVisuals.Tint(_body, ScaffoldTint);
                _patch.color = new Color(.45f, .85f, 1f, .5f);
                return;
            }

            _body.localScale = Vector3.one;
            BuildingVisuals.Tint(_body, _site.Definition.WorldSprite != null
                ? Color.white
                : _site.Definition.BodyColor);
            _patch.color = new Color(1f, 1f, 1f, .12f);
            if (TryGetComponent(out CookingStation station))
            {
                station.enabled = true;
            }
        }
    }
}
