using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Builds the 3D mesh of the mountains inside one terrain chunk from its border-distance grid. Mountains
    /// used to be painted onto the flat ground with a fake south-facing cliff, which only ever looked right
    /// from one side and made every massif read as a slab. Real geometry gets every side, the occlusion of
    /// sprites behind it and the camera's own perspective right for free.
    /// </summary>
    /// <remarks>
    /// The mesh samples the grid more finely than the landform map (<see cref="Subdivision"/>), because a
    /// wall rises its full height in well under a metre and a coarse lattice would step along every diagonal
    /// edge. Only quads touching the mountain are kept; vertices outside it are buried just below the
    /// ground so the foot emerges from the terrain instead of sitting on it.
    /// </remarks>
    public sealed class MountainMeshBuilder
    {
        /// <summary>Mesh vertices per landform texel along each axis.</summary>
        public const int Subdivision = 2;

        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _triangles = new();
        private int[] _vertexIndex = System.Array.Empty<int>();
        private float[] _distances = System.Array.Empty<float>();
        private float[] _excess = System.Array.Empty<float>();

        /// <summary>
        /// Rebuilds <paramref name="mesh"/> in chunk-local space, origin at the chunk's minimum corner.
        /// </summary>
        /// <param name="inside">Signed metres inside the mountain border per landform texel, gutter included.</param>
        /// <param name="elevation">Normalised landform elevation per texel, laid out like <paramref name="inside"/>.</param>
        /// <returns>False when no part of the chunk is mountain; the mesh is then left empty.</returns>
        public bool Build(Mesh mesh, float[] inside, float[] elevation, int textureSize, int gutter,
            int resolution, float chunkSize, float chunkOriginX, float chunkOriginZ, LandformAppearance appearance)
        {
            if (mesh == null)
            {
                throw new System.ArgumentNullException(nameof(mesh));
            }

            mesh.Clear();
            if (inside == null || elevation == null || elevation.Length < inside.Length || resolution <= 0 ||
                chunkSize <= 0f || textureSize <= gutter * 2 + resolution)
            {
                return false;
            }

            int cells = resolution * Subdivision;
            int verticesPerAxis = cells + 1;
            float step = chunkSize / cells;
            EnsureCapacity(verticesPerAxis * verticesPerAxis);

            // Distances first, so each quad can be tested before any vertex is emitted.
            for (int z = 0; z < verticesPerAxis; z++)
            {
                for (int x = 0; x < verticesPerAxis; x++)
                {
                    float texelX = gutter + (float)x / Subdivision;
                    float texelZ = gutter + (float)z / Subdivision;
                    _distances[z * verticesPerAxis + x] = Bilinear(inside, textureSize, texelX, texelZ);
                    _excess[z * verticesPerAxis + x] = Bilinear(elevation, textureSize, texelX, texelZ) - 1f;
                    _vertexIndex[z * verticesPerAxis + x] = -1;
                }
            }

            _vertices.Clear();
            _triangles.Clear();
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int a = z * verticesPerAxis + x;
                    int b = a + 1;
                    int c = a + verticesPerAxis;
                    int d = c + 1;
                    if (_distances[a] <= 0f && _distances[b] <= 0f && _distances[c] <= 0f && _distances[d] <= 0f)
                    {
                        continue;
                    }

                    int ia = Vertex(a, x, z, step, verticesPerAxis, chunkOriginX, chunkOriginZ, appearance);
                    int ib = Vertex(b, x + 1, z, step, verticesPerAxis, chunkOriginX, chunkOriginZ, appearance);
                    int ic = Vertex(c, x, z + 1, step, verticesPerAxis, chunkOriginX, chunkOriginZ, appearance);
                    int id = Vertex(d, x + 1, z + 1, step, verticesPerAxis, chunkOriginX, chunkOriginZ, appearance);
                    // Clockwise seen from above, so the faces point up and out towards the camera.
                    _triangles.Add(ia);
                    _triangles.Add(ic);
                    _triangles.Add(id);
                    _triangles.Add(ia);
                    _triangles.Add(id);
                    _triangles.Add(ib);
                }
            }

            if (_triangles.Count == 0)
            {
                return false;
            }

            mesh.indexFormat = _vertices.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        private int Vertex(int gridIndex, int x, int z, float step, int verticesPerAxis, float chunkOriginX,
            float chunkOriginZ, LandformAppearance appearance)
        {
            int existing = _vertexIndex[gridIndex];
            if (existing >= 0)
            {
                return existing;
            }

            float localX = x * step;
            float localZ = z * step;
            float height = MountainHeightProfile.Height(_distances[gridIndex], _excess[gridIndex],
                chunkOriginX + localX, chunkOriginZ + localZ, appearance);
            // Built taller than nominal so walls read at sprite scale under the steep camera; the shader
            // divides it back out for lighting and texturing. Buried feet stay just under the ground.
            float builtHeight = height > 0f ? height * appearance.VerticalExaggeration : height;
            _vertices.Add(new Vector3(localX, builtHeight, localZ));
            _vertexIndex[gridIndex] = _vertices.Count - 1;
            return _vertices.Count - 1;
        }

        private void EnsureCapacity(int vertexCount)
        {
            if (_distances.Length < vertexCount)
            {
                _distances = new float[vertexCount];
                _excess = new float[vertexCount];
                _vertexIndex = new int[vertexCount];
            }
        }

        private static float Bilinear(float[] grid, int size, float x, float z)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, size - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, size - 1);
            int x1 = Mathf.Min(x0 + 1, size - 1);
            int z1 = Mathf.Min(z0 + 1, size - 1);
            float tx = Mathf.Clamp01(x - x0);
            float tz = Mathf.Clamp01(z - z0);
            float lower = Mathf.Lerp(grid[z0 * size + x0], grid[z0 * size + x1], tx);
            float upper = Mathf.Lerp(grid[z1 * size + x0], grid[z1 * size + x1], tx);
            return Mathf.Lerp(lower, upper, tz);
        }
    }
}
