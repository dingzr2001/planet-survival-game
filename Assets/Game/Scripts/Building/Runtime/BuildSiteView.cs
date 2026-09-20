using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Mining.Runtime;
using PlanetSurvival.Farming.Runtime;
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
        private OxygenReservoir _oxygenReservoir;
        private GameClock _clock;

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
                if (_site.State == BuildState.UnderConstruction)
                {
                    return $"cancel the {structure} ({_site.RemainingSeconds:0}s left)";
                }

                if (_site.OxygenCandle == null)
                {
                    return string.Empty;
                }

                if (_site.OxygenCandle.IsSpent)
                {
                    return "spent oxygen candle";
                }

                if (_clock == null)
                {
                    return "oxygen candle";
                }

                return $"oxygen candle: {OxygenCandleBurn.OxygenLitersPerInterval:0} L / " +
                       $"{OxygenCandleBurn.OutputIntervalGameHours:0.#}h · " +
                       $"{_site.OxygenCandle.RemainingGameHours(_clock.ElapsedDays):0.#}h left";
            }
        }

        public void Bind(BuildSite site, BuildingService service, float cellSize,
            WorldVisualSettings visuals, CookingStationBinding cooking = default,
            MiningDrillBinding mining = default,
            PlanterBoxBinding planter = default,
            OxygenReservoir oxygenReservoir = null, GameClock clock = null)
        {
            _site = site;
            _service = service;
            _cellSize = Mathf.Max(.05f, cellSize);
            _oxygenReservoir = oxygenReservoir;
            _clock = clock;
            BuildableDefinition buildable = site.Definition;

            _body = BuildingVisuals.CreateBody(transform, buildable, _cellSize);

            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.size = new Vector3(
                buildable.Footprint.x * _cellSize,
                BuildingVisuals.BodyHeight(_body, buildable.WorldHeight),
                buildable.Footprint.y * _cellSize);
            _collider.center = Vector3.up * (_collider.size.y * .5f);

            _patch = BuildingVisuals.CreateFootprintPatch(transform, buildable.Footprint, _cellSize);

            if (cooking.IsComplete)
            {
                // The station only becomes usable once the structure stands, so it starts disabled.
                CookingStation station = gameObject.AddComponent<CookingStation>();
                station.Bind(cooking);
                station.enabled = false;
            }

            if (mining.IsComplete)
            {
                MiningDrillStation drill = gameObject.AddComponent<MiningDrillStation>();
                drill.Bind(mining);
                drill.enabled = false;
            }

            if (planter.IsComplete)
            {
                PlanterBoxStation planterStation = gameObject.AddComponent<PlanterBoxStation>();
                planterStation.Bind(planter);
                planterStation.enabled = false;
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
            if (_site == null || _service == null || context.Inventory == null)
            {
                return false;
            }

            return _site.State == BuildState.UnderConstruction ||
                   (_site.OxygenCandle != null && _clock != null && _oxygenReservoir != null);
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) || _site.State != BuildState.UnderConstruction)
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

        private void Update()
        {
            if (_site?.OxygenCandle != null && _oxygenReservoir != null && _clock != null)
            {
                _site.OxygenCandle.Advance(_clock.ElapsedDays, _oxygenReservoir);
            }

            if (_site?.State == BuildState.Completed && _site.PlanterBox != null)
            {
                _site.PlanterBox.Advance(Time.deltaTime);
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
                _patch.enabled = true;
                _patch.color = new Color(.45f, .85f, 1f, .5f);
                return;
            }

            _body.localScale = Vector3.one;
            BuildingVisuals.Tint(_body, _site.Definition.WorldSprite != null
                ? Color.white
                : _site.Definition.BodyColor);

            // The outlined patch marks cells that are spoken for while the site is still a promise. Once the
            // structure stands it is the structure that shows where it is, so the outline only frames it.
            _patch.enabled = false;
            if (_site.OxygenCandle != null && !_site.OxygenCandle.IsIgnited && _clock != null)
            {
                _site.OxygenCandle.Ignite(_clock.ElapsedDays);
            }

            if (TryGetComponent(out CookingStation station))
            {
                station.enabled = true;
            }

            if (TryGetComponent(out MiningDrillStation drill))
            {
                drill.enabled = true;
            }
            if (TryGetComponent(out PlanterBoxStation planter))
            {
                planter.enabled = true;
            }
        }
    }
}
