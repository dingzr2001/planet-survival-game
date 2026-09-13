using System.Collections.Generic;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Draws the terrain patches of one block of tiles. Terrain artwork is a cutout layer laid over the
    /// base regolith rather than a replacement for it, so the ground keeps showing through the gaps.
    /// Every tile of a layer joins one mesh, so a block costs one draw call per terrain it contains
    /// rather than one per tile, and UVs are taken from world space, which is what lets neighbouring
    /// tiles of the same terrain meet without a visible seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainChunkView : MonoBehaviour
    {
        /// <summary>
        /// Height above the base ground disc. The overlay does not write depth, so this only has to lift
        /// it clear of the disc's own surface for the depth test.
        /// </summary>
        public const float SurfaceHeight = .006f;

        /// <summary>
        /// Where the overlay sits among the other transparent things on the floor. Below
        /// <see cref="GroundDecalView.SortingOrder"/>, so an ice sheet resting on rock still draws on top
        /// of it; both stay far below the actors, which sort by camera depth around zero.
        /// </summary>
        public const int SortingOrder = GroundDecalView.SortingOrder - 100;

        /// <summary>
        /// Cells per tile edge used to draw the patch outline. The dig grid is unchanged — this only
        /// decides how finely the drawn edge can follow the terrain field. At one cell per tile the
        /// outline is a staircase of three-metre steps that slices boulders down the middle; four
        /// subdivisions put the steps below a metre, where the cut reads as rubble instead of a crop.
        /// </summary>
        private const int VisualSubdivisions = 4;

        private readonly List<GameObject> _layerObjects = new();
        private readonly List<Mesh> _meshes = new();
        private readonly List<Material> _materials = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<int> _triangles = new();

        /// <summary>Number of terrain meshes currently drawn, one per terrain present in the block.</summary>
        public int LayerMeshCount => _meshes.Count;

        /// <param name="origin">The block's lowest tile, towards negative X and Z.</param>
        /// <param name="sizeInTiles">Tiles per side of the block.</param>
        public void Rebuild(TerrainTileMap map, TerrainTileCoordinate origin, int sizeInTiles)
        {
            ReleaseGeneratedObjects();
            if (map == null || sizeInTiles <= 0)
            {
                return;
            }

            IReadOnlyList<TerrainPatchLayer> layers = map.Layers;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                TerrainSurfaceDefinition surface = layers[layerIndex].Surface;
                if (surface != null)
                {
                    BuildLayerMesh(map, origin, sizeInTiles, layerIndex, surface);
                }
            }
        }

        private void BuildLayerMesh(TerrainTileMap map, TerrainTileCoordinate origin, int sizeInTiles,
            int layerIndex, TerrainSurfaceDefinition surface)
        {
            float tileSize = map.TileSize;
            float originX = origin.MinX(tileSize);
            float originZ = origin.MinZ(tileSize);
            float uvScale = 1f / surface.TextureTileSize;
            float cellSize = tileSize / VisualSubdivisions;
            int cellsPerSide = sizeInTiles * VisualSubdivisions;
            _vertices.Clear();
            _uv.Clear();
            _triangles.Clear();

            for (int z = 0; z < cellsPerSide; z++)
            {
                // Runs of neighbouring cells become one quad. A solid patch costs about what whole tiles
                // used to, and only the ragged border pays for the finer outline.
                int runStart = -1;
                for (int x = 0; x <= cellsPerSide; x++)
                {
                    bool covered = x < cellsPerSide
                                   && IsCellCovered(map, layerIndex, originX, originZ, cellSize, x, z);
                    if (covered)
                    {
                        if (runStart < 0)
                        {
                            runStart = x;
                        }

                        continue;
                    }

                    if (runStart >= 0)
                    {
                        AppendCellRun(originX, originZ, cellSize, runStart, x, z, uvScale);
                        runStart = -1;
                    }
                }
            }

            if (_vertices.Count == 0)
            {
                return;
            }

            var mesh = new Mesh { name = $"Terrain {surface.TerrainId}" };
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uv);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            // Alpha-blended: the artwork is loose rock on a transparent sheet, and the regolith below has
            // to keep showing through. An opaque shader would paint the transparent pixels black.
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) { name = $"Runtime Terrain {surface.TerrainId}" };
            material.mainTexture = surface.Texture;
            _materials.Add(material);

            var layerObject = new GameObject(surface.DisplayName);
            _layerObjects.Add(layerObject);
            layerObject.transform.SetParent(transform, false);
            layerObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = layerObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Transparent geometry is ordered by sorting order before distance, so without this the
            // overlay would land at zero and cover the ground decals it is supposed to sit under.
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static bool IsCellCovered(TerrainTileMap map, int layerIndex, float originX, float originZ,
            float cellSize, int x, int z)
        {
            return map.GetVisualLayerIndex(
                originX + (x + .5f) * cellSize, originZ + (z + .5f) * cellSize) == layerIndex;
        }

        /// <summary>
        /// Adds one quad spanning cells <paramref name="startX"/> up to <paramref name="endX"/> of a row.
        /// Positions are relative to the block so distant blocks keep their float precision, while UVs
        /// stay absolute so the texture runs on unbroken across cell and block borders alike.
        /// </summary>
        private void AppendCellRun(float originX, float originZ, float cellSize, int startX, int endX,
            int z, float uvScale)
        {
            float minWorldX = originX + startX * cellSize;
            float maxWorldX = originX + endX * cellSize;
            float minWorldZ = originZ + z * cellSize;
            float maxWorldZ = minWorldZ + cellSize;
            float localMinX = minWorldX - originX;
            float localMaxX = maxWorldX - originX;
            float localMinZ = minWorldZ - originZ;
            float localMaxZ = maxWorldZ - originZ;
            int baseIndex = _vertices.Count;

            _vertices.Add(new Vector3(localMinX, 0f, localMinZ));
            _vertices.Add(new Vector3(localMaxX, 0f, localMinZ));
            _vertices.Add(new Vector3(localMaxX, 0f, localMaxZ));
            _vertices.Add(new Vector3(localMinX, 0f, localMaxZ));

            _uv.Add(new Vector2(minWorldX * uvScale, minWorldZ * uvScale));
            _uv.Add(new Vector2(maxWorldX * uvScale, minWorldZ * uvScale));
            _uv.Add(new Vector2(maxWorldX * uvScale, maxWorldZ * uvScale));
            _uv.Add(new Vector2(minWorldX * uvScale, maxWorldZ * uvScale));

            // Wound so the quad faces straight up; the camera never sees the surface from below.
            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 3);
            _triangles.Add(baseIndex + 2);
        }

        private void OnDestroy()
        {
            ReleaseGeneratedObjects();
        }

        private void ReleaseGeneratedObjects()
        {
            for (int i = 0; i < _layerObjects.Count; i++)
            {
                GameObject layerObject = _layerObjects[i];
                if (layerObject == null)
                {
                    continue;
                }

                // Play-mode destruction only lands at the end of the frame. Hiding the object first keeps
                // the retiring meshes from z-fighting the replacements a rebuild adds in the same frame.
                layerObject.SetActive(false);
                DestroyRuntimeObject(layerObject);
            }

            _layerObjects.Clear();
            for (int i = 0; i < _meshes.Count; i++)
            {
                DestroyRuntimeObject(_meshes[i]);
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                DestroyRuntimeObject(_materials[i]);
            }

            _meshes.Clear();
            _materials.Clear();
        }

        private static void DestroyRuntimeObject(Object instance)
        {
            if (instance == null)
            {
                return;
            }

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
