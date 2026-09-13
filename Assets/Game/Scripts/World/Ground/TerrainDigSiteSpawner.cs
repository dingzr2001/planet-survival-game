using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Gives the tiles within reach of the player something the interactor can find. Terrain covers the
    /// whole surface, so only the handful of tiles the player could actually swing at carry a collider;
    /// the rest is drawn geometry with nothing attached.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainDigSiteSpawner : MonoBehaviour
    {
        // The tile under the player plus its eight neighbours. For any tile at least as wide as the
        // interactor's reach that covers everything reachable, whichever corner the player stands in.
        private const int NeighbourhoodRadiusInTiles = 1;

        /// <summary>Height of the trigger slab over a tile. Terrain is ground, not something to walk into.</summary>
        private const float SiteColliderHeight = .1f;

        /// <summary>
        /// Fraction of the tile the trigger covers, centred. A slab spanning the whole tile would sit at
        /// zero distance from a player standing anywhere on it and so always win the interactor's
        /// closest-first pick, hiding any resource node sharing the tile. Shrinking it keeps "dig where
        /// you stand" in the middle of the tile while letting a genuinely adjacent object win at the
        /// edges, and still leaves every tile within the interactor's reach from any corner.
        /// </summary>
        private const float SiteColliderTileFraction = .6f;

        private readonly Dictionary<TerrainTileCoordinate, TerrainDigSite> _sites = new();
        private readonly List<TerrainTileCoordinate> _tileBuffer = new();
        private TerrainTileMap _map;
        private Transform _target;
        private TerrainTileCoordinate _center;
        private bool _hasCenter;
        private bool _dirty;

        public int ActiveSiteCount => _sites.Count;

        public void Configure(TerrainTileMap map)
        {
            UnsubscribeFromMap();
            ClearSites();
            _map = map;
            if (_map != null)
            {
                _map.TileChanged += OnTileChanged;
            }

            Refresh();
        }

        /// <summary>Sets the transform the sites follow; passing null removes them.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            _hasCenter = false;
            Refresh();
        }

        private void Update()
        {
            if (_map == null || _target == null)
            {
                return;
            }

            TerrainTileCoordinate center = _map.TileAt(_target.position);
            if (!_dirty && _hasCenter && center.Equals(_center))
            {
                return;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            UnsubscribeFromMap();
        }

        private void OnTileChanged(TerrainTileCoordinate tile) => _dirty = true;

        private void Refresh()
        {
            _dirty = false;
            if (_map == null || _target == null)
            {
                ClearSites();
                return;
            }

            _center = _map.TileAt(_target.position);
            _hasCenter = true;
            RetireSitesOutsideNeighbourhood();

            for (int x = -NeighbourhoodRadiusInTiles; x <= NeighbourhoodRadiusInTiles; x++)
            {
                for (int z = -NeighbourhoodRadiusInTiles; z <= NeighbourhoodRadiusInTiles; z++)
                {
                    var tile = new TerrainTileCoordinate(_center.X + x, _center.Z + z);
                    // A site already standing on a still-diggable tile is kept rather than rebuilt, so a
                    // dig in progress survives the change event its own first swing raises.
                    if (_map.IsDiggable(tile) && !_sites.ContainsKey(tile))
                    {
                        _sites.Add(tile, CreateSite(tile));
                    }
                }
            }
        }

        private void RetireSitesOutsideNeighbourhood()
        {
            _tileBuffer.Clear();
            foreach (KeyValuePair<TerrainTileCoordinate, TerrainDigSite> entry in _sites)
            {
                bool inReach = Mathf.Abs(entry.Key.X - _center.X) <= NeighbourhoodRadiusInTiles
                               && Mathf.Abs(entry.Key.Z - _center.Z) <= NeighbourhoodRadiusInTiles;
                if (!inReach || !_map.IsDiggable(entry.Key) || entry.Value == null)
                {
                    _tileBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < _tileBuffer.Count; i++)
            {
                Retire(_tileBuffer[i]);
            }

            _tileBuffer.Clear();
        }

        private TerrainDigSite CreateSite(TerrainTileCoordinate tile)
        {
            float tileSize = _map.TileSize;
            TerrainSurfaceDefinition surface = _map.GetSurface(tile);
            var siteObject = new GameObject($"Dig Site - {surface.DisplayName} {tile}");
            siteObject.transform.SetParent(transform);
            siteObject.transform.position = new Vector3(tile.CenterX(tileSize), 0f, tile.CenterZ(tileSize));

            var collider = siteObject.AddComponent<BoxCollider>();
            float colliderWidth = tileSize * SiteColliderTileFraction;
            collider.size = new Vector3(colliderWidth, SiteColliderHeight, colliderWidth);
            collider.center = Vector3.up * (SiteColliderHeight * .5f);
            // Terrain is walked over, never around: the volume only exists to carry the interaction, and
            // the interactor's overlap query includes triggers.
            collider.isTrigger = true;

            TerrainDigSite site = siteObject.AddComponent<TerrainDigSite>();
            site.Configure(_map, tile);
            return site;
        }

        private void Retire(TerrainTileCoordinate tile)
        {
            if (!_sites.TryGetValue(tile, out TerrainDigSite site))
            {
                return;
            }

            _sites.Remove(tile);
            if (site != null)
            {
                DestroyRuntimeObject(site.gameObject);
            }
        }

        private void ClearSites()
        {
            _tileBuffer.Clear();
            _tileBuffer.AddRange(_sites.Keys);
            for (int i = 0; i < _tileBuffer.Count; i++)
            {
                Retire(_tileBuffer[i]);
            }

            _tileBuffer.Clear();
            _hasCenter = false;
        }

        private void UnsubscribeFromMap()
        {
            if (_map != null)
            {
                _map.TileChanged -= OnTileChanged;
            }
        }

        private static void DestroyRuntimeObject(GameObject instance)
        {
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
