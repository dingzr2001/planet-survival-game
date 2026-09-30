using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Scatters loose rocks on the ground in one terrain chunk, as a single mesh of billboards: rocks fallen
    /// along the foot of the mountains, and ejecta thrown out around craters. Painted onto the ground, a rock's far half lay under the mountain mesh and was cut
    /// off at the wall; standing billboards whose depth is taken at their foot sit in front of the wall like
    /// any other object in the world.
    /// </summary>
    /// <remarks>
    /// Every one-metre cell may hold one rock, jittered within its cell. A cell holds a fallen rock when that
    /// point lies on the ground within the apron outside a mountain, thinning out and shrinking away from the
    /// foot; failing that, it may hold a smaller ejecta rock, as likely as the crater mark there is thick. Each rock is four vertices sharing its foot position; the shader expands them towards the
    /// camera. Rocks are emitted far to near so nearer ones overlap farther ones.
    /// </remarks>
    public sealed class TalusMeshBuilder
    {
        public const float CellSize = 1f;

        private const int JitterXSalt = 0x21;
        private const int JitterZSalt = 0x22;
        private const int KeepSalt = 0x23;
        private const int SizeSalt = 0x24;
        private const int ArtSalt = 0x25;
        private const int EjectaKeepSalt = 0x26;
        private const int WidthSeed = 0x5f3759df;

        private static readonly Vector2[] Corners =
        {
            new(-.5f, -.5f), new(.5f, -.5f), new(-.5f, .5f), new(.5f, .5f)
        };

        private readonly List<Rock> _rocks = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _corners = new();
        private readonly List<Vector2> _sizeAndSlice = new();
        private readonly List<int> _triangles = new();

        public int RockCount => _rocks.Count;

        /// <param name="inside">Signed metres inside the mountain border per landform texel, gutter included.</param>
        /// <param name="craterMarks">
        /// Crater mark per texel (<see cref="CraterMarkings"/>), laid out like <paramref name="inside"/>; null
        /// scatters no ejecta.
        /// </param>
        /// <param name="firstSlice">Artwork-array slice of the first rock cutout.</param>
        /// <param name="sliceCount">Number of rock cutouts; zero builds nothing.</param>
        /// <returns>False when no rock lies in the chunk; the mesh is then left empty.</returns>
        public bool Build(Mesh mesh, float[] inside, float[] craterMarks, int textureSize, int gutter, int resolution,
            float chunkSize, float chunkOriginX, float chunkOriginZ, LandformAppearance appearance, int worldSeed,
            int firstSlice, int sliceCount)
        {
            if (mesh == null)
            {
                throw new System.ArgumentNullException(nameof(mesh));
            }

            mesh.Clear();
            _rocks.Clear();
            bool scattersEjecta = craterMarks != null && appearance != null && appearance.EjectaRockDensity > 0f;
            if (inside == null || appearance == null || (appearance.TalusWidth <= 0f && !scattersEjecta) ||
                sliceCount <= 0 || resolution <= 0 || chunkSize <= 0f)
            {
                return false;
            }

            float step = chunkSize / resolution;
            int firstCellX = Mathf.CeilToInt(chunkOriginX / CellSize - .5f);
            int firstCellZ = Mathf.CeilToInt(chunkOriginZ / CellSize - .5f);
            int lastCellX = Mathf.CeilToInt((chunkOriginX + chunkSize) / CellSize - .5f) - 1;
            int lastCellZ = Mathf.CeilToInt((chunkOriginZ + chunkSize) / CellSize - .5f) - 1;
            for (int cellZ = firstCellZ; cellZ <= lastCellZ; cellZ++)
            {
                for (int cellX = firstCellX; cellX <= lastCellX; cellX++)
                {
                    TryPlaceRock(cellX, cellZ, inside, scattersEjecta ? craterMarks : null, textureSize, gutter, step,
                        chunkOriginX, chunkOriginZ, appearance, worldSeed, firstSlice, sliceCount);
                }
            }

            if (_rocks.Count == 0)
            {
                return false;
            }

            // Far to near: rows further north first, so a nearer rock is drawn over the one behind it.
            _rocks.Sort((a, b) => b.Position.z.CompareTo(a.Position.z));
            _vertices.Clear();
            _corners.Clear();
            _sizeAndSlice.Clear();
            _triangles.Clear();
            for (int i = 0; i < _rocks.Count; i++)
            {
                int first = _vertices.Count;
                for (int corner = 0; corner < Corners.Length; corner++)
                {
                    _vertices.Add(_rocks[i].Position);
                    _corners.Add(Corners[corner]);
                    _sizeAndSlice.Add(new Vector2(_rocks[i].Size, _rocks[i].Slice));
                }

                _triangles.Add(first);
                _triangles.Add(first + 2);
                _triangles.Add(first + 3);
                _triangles.Add(first);
                _triangles.Add(first + 3);
                _triangles.Add(first + 1);
            }

            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _corners);
            mesh.SetUVs(1, _sizeAndSlice);
            mesh.SetTriangles(_triangles, 0);
            // The shader grows each quad towards the camera, so pad the bounds by the largest rock.
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(CellSize * 4f);
            mesh.bounds = bounds;
            return true;
        }

        private void TryPlaceRock(int cellX, int cellZ, float[] inside, float[] craterMarks, int textureSize,
            int gutter, float step, float chunkOriginX, float chunkOriginZ, LandformAppearance appearance,
            int worldSeed, int firstSlice, int sliceCount)
        {
            float worldX = (cellX + .5f + (GradientNoise.Hash01(worldSeed + JitterXSalt, cellX, cellZ) - .5f) * .8f)
                           * CellSize;
            float worldZ = (cellZ + .5f + (GradientNoise.Hash01(worldSeed + JitterZSalt, cellX, cellZ) - .5f) * .8f)
                           * CellSize;
            float texelX = gutter + (worldX - chunkOriginX) / step;
            float texelZ = gutter + (worldZ - chunkOriginZ) / step;
            float outside = -Bilinear(inside, textureSize, texelX, texelZ);
            if (outside < .05f)
            {
                return;
            }

            float size = FallenRockSize(cellX, cellZ, worldX, worldZ, outside, appearance, worldSeed);
            if (size <= 0f && craterMarks != null)
            {
                size = EjectaRockSize(cellX, cellZ, Bilinear(craterMarks, textureSize, texelX, texelZ), appearance,
                    worldSeed);
            }

            if (size <= 0f)
            {
                return;
            }

            int art = Mathf.Min((int)(GradientNoise.Hash01(worldSeed + ArtSalt, cellX, cellZ) * sliceCount),
                sliceCount - 1);
            _rocks.Add(new Rock(new Vector3(worldX - chunkOriginX, 0f, worldZ - chunkOriginZ), size, firstSlice + art));
        }

        /// <summary>Size of the rock fallen from a mountain into this cell, or zero for none.</summary>
        private static float FallenRockSize(int cellX, int cellZ, float worldX, float worldZ, float outside,
            LandformAppearance appearance, int worldSeed)
        {
            if (appearance.TalusWidth <= 0f)
            {
                return 0f;
            }

            float wander = GradientNoise.Sample(WidthSeed, worldX * .2f, worldZ * .2f) * .5f + .5f;
            float width = appearance.TalusWidth * Mathf.Lerp(.7f, 1.25f, wander);
            if (outside > width)
            {
                return 0f;
            }

            float density = 1f - Mathf.Clamp01(outside / width);
            if (GradientNoise.Hash01(worldSeed + KeepSalt, cellX, cellZ) > density * .75f + .2f)
            {
                return 0f;
            }

            // The biggest rocks heap against the wall, smaller ones further out.
            return CellSize * Mathf.Lerp(.6f, 1f, GradientNoise.Hash01(worldSeed + SizeSalt, cellX, cellZ))
                   * Mathf.Lerp(.8f, 1.9f, density);
        }

        /// <summary>Size of the ejecta rock thrown into this cell, or zero for none.</summary>
        private static float EjectaRockSize(int cellX, int cellZ, float craterMark, LandformAppearance appearance,
            int worldSeed)
        {
            // Negative marks are crater floors, which keep only their dust.
            float keepChance = craterMark * appearance.EjectaRockDensity;
            if (craterMark <= 0f || GradientNoise.Hash01(worldSeed + EjectaKeepSalt, cellX, cellZ) >= keepChance)
            {
                return 0f;
            }

            // Ejecta is finer than cliff rubble, and coarsest where it lies thickest, by the rim.
            return CellSize * Mathf.Lerp(.35f, .75f, GradientNoise.Hash01(worldSeed + SizeSalt, cellX, cellZ))
                   * Mathf.Lerp(.8f, 1.3f, craterMark);
        }

        private static float Bilinear(float[] grid, int size, float x, float z)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, size - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, size - 1);
            int x1 = Mathf.Min(x0 + 1, size - 1);
            int z1 = Mathf.Min(z0 + 1, size - 1);
            float tx = Mathf.Clamp01(x - x0);
            float tz = Mathf.Clamp01(z - z0);
            return Mathf.Lerp(
                Mathf.Lerp(grid[z0 * size + x0], grid[z0 * size + x1], tx),
                Mathf.Lerp(grid[z1 * size + x0], grid[z1 * size + x1], tx),
                tz);
        }

        private readonly struct Rock
        {
            public Rock(Vector3 position, float size, int slice)
            {
                Position = position;
                Size = size;
                Slice = slice;
            }

            public Vector3 Position { get; }
            public float Size { get; }
            public int Slice { get; }
        }
    }
}
