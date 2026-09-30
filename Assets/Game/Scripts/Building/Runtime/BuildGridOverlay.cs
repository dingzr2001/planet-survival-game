using System.Collections.Generic;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// Draws the session's construction grid over the visible ground and lets the player toggle it for
    /// placement and diagnostics. The overlay only owns presentation; snapping and occupancy remain in
    /// <see cref="BuildGrid"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BuildGridOverlay : MonoBehaviour
    {
        private const int CellPadding = 1;
        private const int SortingOrder = -24000;
        private const float GroundOffset = .025f;
        private const float SunkenBoundsDepth = 60f;

        [SerializeField, Tooltip("Keyboard shortcut that shows or hides the construction grid.")]
        private KeyCode _toggleKey = KeyCode.G;

        [SerializeField, Tooltip("Grid line colour. Alpha controls how strongly the terrain shows through.")]
        private Color _lineColor = new(.72f, .88f, 1f, .32f);

        [SerializeField, Tooltip("Useful for capture or dedicated build scenes; normal gameplay starts hidden.")]
        private bool _showAtStartup;

        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _indices = new();

        private BuildGrid _grid;
        private Camera _camera;
        private IGroundSurface _surface;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private bool _isVisible;
        private int _minimumBoundaryX = int.MinValue;
        private int _maximumBoundaryX = int.MinValue;
        private int _minimumBoundaryZ = int.MinValue;
        private int _maximumBoundaryZ = int.MinValue;

        public bool IsVisible => _isVisible;
        public KeyCode ToggleKey => _toggleKey;

        /// <summary>
        /// The drawn ground the grid lies on and the view is measured against; null is flat ground. The
        /// lines themselves follow it in the shader.
        /// </summary>
        public void SetGroundSurface(IGroundSurface surface)
        {
            _surface = surface;
            if (_isVisible)
            {
                Refresh(true);
            }
        }

        public void Bind(BuildGrid grid, Camera targetCamera)
        {
            if (grid == null)
            {
                Debug.LogError($"{nameof(BuildGridOverlay)} needs a build grid.", this);
                enabled = false;
                return;
            }

            _grid = grid;
            _camera = targetCamera;
            EnsurePresentation();
            SetVisible(_showAtStartup);
        }

        public void Toggle()
        {
            SetVisible(!_isVisible);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            EnsurePresentation();
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = visible;
            }

            if (visible)
            {
                Refresh(true);
            }
        }

        private void Update()
        {
            if (_grid != null && Input.GetKeyDown(_toggleKey))
            {
                Toggle();
            }
        }

        private void LateUpdate()
        {
            Refresh(false);
        }

        private void OnDisable()
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = _isVisible;
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_material);
            DestroyRuntimeObject(_mesh);
        }

        private void EnsurePresentation()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                _meshRenderer.sortingOrder = SortingOrder;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Runtime Build Grid" };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_material != null)
            {
                return;
            }

            // Drawn at the height of the ground under each vertex, so the grid follows craters.
            Shader shader = SurfaceSpriteMaterial.Shader;
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                Debug.LogError($"{nameof(BuildGridOverlay)} could not find a transparent runtime shader.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader)
            {
                name = "Runtime Build Grid",
                color = _lineColor,
                hideFlags = HideFlags.HideAndDontSave
            };
            _meshRenderer.sharedMaterial = _material;
        }

        private void Refresh(bool force)
        {
            if (!_isVisible || _grid == null || _mesh == null || _meshRenderer == null)
            {
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            if (_camera == null || !TryGetVisibleBoundaries(
                    out int minimumBoundaryX, out int maximumBoundaryX,
                    out int minimumBoundaryZ, out int maximumBoundaryZ))
            {
                _meshRenderer.enabled = false;
                return;
            }

            _meshRenderer.enabled = true;
            if (!force && minimumBoundaryX == _minimumBoundaryX && maximumBoundaryX == _maximumBoundaryX &&
                minimumBoundaryZ == _minimumBoundaryZ && maximumBoundaryZ == _maximumBoundaryZ)
            {
                return;
            }

            _minimumBoundaryX = minimumBoundaryX;
            _maximumBoundaryX = maximumBoundaryX;
            _minimumBoundaryZ = minimumBoundaryZ;
            _maximumBoundaryZ = maximumBoundaryZ;
            RebuildMesh();
        }

        private bool TryGetVisibleBoundaries(out int minimumBoundaryX, out int maximumBoundaryX,
            out int minimumBoundaryZ, out int maximumBoundaryZ)
        {
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;

            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    Ray ray = _camera.ViewportPointToRay(new Vector3(x, y));
                    if (!GroundSurfaceRaycast.TryIntersect(_surface, ray, _grid.Origin.y, out Vector3 point))
                    {
                        minimumBoundaryX = maximumBoundaryX = minimumBoundaryZ = maximumBoundaryZ = 0;
                        return false;
                    }

                    minimumX = Mathf.Min(minimumX, point.x);
                    maximumX = Mathf.Max(maximumX, point.x);
                    minimumZ = Mathf.Min(minimumZ, point.z);
                    maximumZ = Mathf.Max(maximumZ, point.z);
                }
            }

            float cellSize = _grid.CellSize;
            Vector3 origin = _grid.Origin;
            minimumBoundaryX = Mathf.FloorToInt((minimumX - origin.x) / cellSize) - CellPadding;
            maximumBoundaryX = Mathf.CeilToInt((maximumX - origin.x) / cellSize) + CellPadding;
            minimumBoundaryZ = Mathf.FloorToInt((minimumZ - origin.z) / cellSize) - CellPadding;
            maximumBoundaryZ = Mathf.CeilToInt((maximumZ - origin.z) / cellSize) + CellPadding;
            return true;
        }

        private void RebuildMesh()
        {
            _vertices.Clear();
            _indices.Clear();

            float cellSize = _grid.CellSize;
            Vector3 origin = _grid.Origin;
            float minimumWorldX = origin.x + _minimumBoundaryX * cellSize;
            float maximumWorldX = origin.x + _maximumBoundaryX * cellSize;
            float minimumWorldZ = origin.z + _minimumBoundaryZ * cellSize;
            float maximumWorldZ = origin.z + _maximumBoundaryZ * cellSize;
            float height = origin.y + GroundOffset;

            for (int x = _minimumBoundaryX; x <= _maximumBoundaryX; x++)
            {
                float worldX = origin.x + x * cellSize;
                AddLine(
                    new Vector3(worldX, height, minimumWorldZ),
                    new Vector3(worldX, height, maximumWorldZ));
            }

            for (int z = _minimumBoundaryZ; z <= _maximumBoundaryZ; z++)
            {
                float worldZ = origin.z + z * cellSize;
                AddLine(
                    new Vector3(minimumWorldX, height, worldZ),
                    new Vector3(maximumWorldX, height, worldZ));
            }

            _mesh.Clear();
            _mesh.indexFormat = _vertices.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
            // The shader draws lines down into craters, below the flat mesh; keep them from being culled.
            Bounds bounds = _mesh.bounds;
            bounds.Encapsulate(bounds.min + Vector3.down * SunkenBoundsDepth);
            _mesh.bounds = bounds;
        }

        /// <summary>
        /// Adds a grid line as one segment per cell, so the shader can bend it into a crater at every cell
        /// corner instead of stretching one straight segment across the dip.
        /// </summary>
        private void AddLine(Vector3 worldStart, Vector3 worldEnd)
        {
            int segments = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(worldStart, worldEnd) / _grid.CellSize));
            int startIndex = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                _vertices.Add(transform.InverseTransformPoint(Vector3.Lerp(worldStart, worldEnd, (float)i / segments)));
                if (i > 0)
                {
                    _indices.Add(startIndex + i - 1);
                    _indices.Add(startIndex + i);
                }
            }
        }

        private static void DestroyRuntimeObject(Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
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
