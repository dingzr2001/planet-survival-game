using System.Collections.Generic;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.UI.Cooking;
using PlanetSurvival.World.Generation;
using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// Drives placement in the world: it snaps the selected structure to the build grid under the mouse,
    /// colours the preview by whether the spot works, and turns a click into a construction site. It also
    /// keeps the scene in sync with the session-owned <see cref="BuildingService"/>, so sites placed before
    /// a trip into the landing pod are still standing — and still building — on the way out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        private static readonly Color ValidTint = new(.45f, 1f, .55f, .55f);
        private static readonly Color InvalidTint = new(1f, .38f, .32f, .55f);

        [SerializeField, Min(1f), Tooltip("How far from the player a structure may be placed, in world units.")]
        private float _placementReach = 10f;

        private readonly Dictionary<BuildSite, GameObject> _siteObjects = new();

        private BuildingService _service;
        private PlayerInventory _playerInventory;
        private BuildingCatalog _catalog;
        private WorldVisualSettings _visuals;
        private GameSessionState _session;
        private CookingView _cookingView;
        private Transform _player;
        private Camera _camera;

        private BuildableDefinition _selected;
        private GameObject _ghost;
        private Transform _ghostBody;
        private SpriteRenderer _ghostPatch;
        private BuildFootprint _footprint;
        private BuildResult _preview = BuildResult.Success();

        public BuildingService Service => _service;
        public BuildingCatalog Catalog => _catalog;
        public bool IsPlacing => _selected != null;
        public BuildableDefinition Selected => _selected;
        public BuildFootprint Footprint => _footprint;

        /// <summary>Why the current spot is rejected. Succeeded while the preview is green.</summary>
        public BuildResult Preview => _preview;

        public void Bind(BuildingService service, PlayerInventory playerInventory, BuildingCatalog catalog,
            WorldVisualSettings visuals = null, GameSessionState session = null, CookingView cookingView = null)
        {
            if (service == null || playerInventory == null)
            {
                Debug.LogError($"{nameof(BuildingPlacementController)} needs a building service and the player backpack.", this);
                enabled = false;
                return;
            }

            Unsubscribe();
            _service = service;
            _playerInventory = playerInventory;
            _player = playerInventory.transform;
            _catalog = catalog;
            _visuals = visuals;
            _session = session;
            _cookingView = cookingView;
            _service.SitePlaced += CreateSiteObject;
            _service.SiteRemoved += DestroySiteObject;

            // Sites survive scene changes inside the session, so the world is rebuilt from what it holds.
            for (int i = 0; i < _service.Sites.Count; i++)
            {
                CreateSiteObject(_service.Sites[i]);
            }
        }

        /// <summary>Enters placement mode with the given structure attached to the cursor.</summary>
        public bool BeginPlacement(BuildableDefinition buildable)
        {
            if (_service == null || buildable == null || !buildable.IsValid(out string error))
            {
                Debug.LogWarning($"'{(buildable != null ? buildable.name : "null")}' cannot be placed.", this);
                return false;
            }

            CancelPlacement();
            _selected = buildable;
            _ghost = new GameObject($"Build Preview - {buildable.DisplayName}");
            _ghost.transform.SetParent(transform, false);
            _ghostBody = BuildingVisuals.CreateBody(_ghost.transform, buildable, _service.Grid.CellSize);
            _ghostPatch = BuildingVisuals.CreateFootprintPatch(_ghost.transform, buildable.Footprint,
                _service.Grid.CellSize);
            UpdatePreview();
            return true;
        }

        public void CancelPlacement()
        {
            _selected = null;
            _preview = BuildResult.Success();
            if (_ghost != null)
            {
                Destroy(_ghost);
            }

            _ghost = null;
            _ghostBody = null;
            _ghostPatch = null;
        }

        /// <summary>
        /// Starts construction at the previewed spot. Placement mode stays active while the backpack can
        /// still pay for another one, so a row of walls does not need a trip through the panel per wall.
        /// </summary>
        public BuildResult ConfirmPlacement()
        {
            if (_selected == null)
            {
                return BuildResult.Fail(BuildFailure.InvalidBuildable, "No structure is selected.");
            }

            UpdatePreview();
            if (!_preview.Succeeded)
            {
                return _preview;
            }

            BuildableDefinition buildable = _selected;
            BuildResult result = _service.TryPlace(buildable, _footprint, out BuildSite _);
            if (!result.Succeeded)
            {
                return result;
            }

            _playerInventory.RefreshQuickBarAssignments();
            if (!_service.CanAfford(buildable))
            {
                CancelPlacement();
            }

            return result;
        }

        private void Update()
        {
            if (_service == null)
            {
                return;
            }

            _service.Advance(Time.deltaTime);
            if (!IsPlacing || Time.timeScale <= 0f)
            {
                return;
            }

            UpdatePreview();
            if (Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_service == null)
            {
                return;
            }

            _service.SitePlaced -= CreateSiteObject;
            _service.SiteRemoved -= DestroySiteObject;
            _siteObjects.Clear();
        }

        /// <summary>Snaps the ghost to the grid cell under the mouse and recolours it.</summary>
        private void UpdatePreview()
        {
            if (_selected == null || !TryGetCursorGroundPosition(out Vector3 ground))
            {
                return;
            }

            BuildGrid grid = _service.Grid;
            _footprint = grid.CreateFootprint(ground, _selected.Footprint);
            Vector3 center = grid.Center(_footprint);
            _preview = _service.CanPlace(_selected, _footprint);
            if (_preview.Succeeded && !IsWithinReach(center))
            {
                _preview = BuildResult.Fail(BuildFailure.OutOfReach, "That spot is out of reach.");
            }

            // Building on top of yourself would trap the player inside a solid structure.
            if (_preview.Succeeded && _player != null && _footprint.Contains(grid.WorldToCell(_player.position)))
            {
                _preview = BuildResult.Fail(BuildFailure.Blocked, "You are standing there.");
            }

            if (_ghost == null)
            {
                return;
            }

            _ghost.transform.position = center;
            Color tint = _preview.Succeeded ? ValidTint : InvalidTint;
            BuildingVisuals.Tint(_ghostBody, tint);
            _ghostPatch.color = new Color(tint.r, tint.g, tint.b, .45f);
        }

        private bool IsWithinReach(Vector3 position)
        {
            if (_player == null)
            {
                return true;
            }

            Vector3 offset = position - _player.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _placementReach * _placementReach;
        }

        /// <summary>Projects the mouse onto the grid plane, which stays reliable where the ground has no collider.</summary>
        private bool TryGetCursorGroundPosition(out Vector3 position)
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                position = default;
                return false;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, _service.Grid.Origin.y, 0f));
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float distance))
            {
                position = default;
                return false;
            }

            position = ray.GetPoint(distance);
            return true;
        }

        private void CreateSiteObject(BuildSite site)
        {
            if (site == null || _siteObjects.ContainsKey(site))
            {
                return;
            }

            var siteObject = new GameObject($"Building - {site.Definition.DisplayName}");
            siteObject.transform.SetParent(transform, false);
            siteObject.transform.position = _service.Grid.Center(site.Footprint);
            siteObject.AddComponent<BuildSiteView>().Bind(
                site, _service, _service.Grid.CellSize, _visuals, CreateCookingBinding(site));
            _siteObjects.Add(site, siteObject);
        }

        private void DestroySiteObject(BuildSite site)
        {
            if (site == null || !_siteObjects.TryGetValue(site, out GameObject siteObject))
            {
                return;
            }

            _siteObjects.Remove(site);
            if (siteObject != null)
            {
                Destroy(siteObject);
            }
        }

        /// <summary>
        /// A built appliance cooks in its own session-owned slot, keyed by the site, so two ovens never
        /// share one pot and each keeps cooking while the player is elsewhere.
        /// </summary>
        private CookingStationBinding CreateCookingBinding(BuildSite site)
        {
            if (site.Definition.CookingStation == null || _cookingView == null || _session == null)
            {
                return default;
            }

            string stationId = $"{site.Definition.CookingStation.StationId}.{site.SiteId}";
            return new CookingStationBinding(
                site.Definition.CookingStation, _session.GetCookingProcess(stationId), _cookingView);
        }
    }
}
