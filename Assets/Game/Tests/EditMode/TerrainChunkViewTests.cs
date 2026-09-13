using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainChunkViewTests
    {
        private const float TileSize = 3f;
        private const int ChunkSizeInTiles = 4;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Rebuild_DrawsOneMeshPerTerrainRatherThanOnePerTile()
        {
            TerrainTileMap map = CreateMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Assert.That(view.LayerMeshCount, Is.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
        }

        [Test]
        public void Rebuild_MergesNeighbouringCellsSoASolidPatchStaysCheap()
        {
            TerrainTileMap map = CreateMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            // Every cell of this block is covered, so each row collapses into a single quad rather than
            // paying four vertices per subdivision cell.
            Mesh mesh = view.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(ChunkSizeInTiles * ChunkSizeInTiles * 4));
        }

        [Test]
        public void Rebuild_DrawsTheOverlayBlendedSoTheRegolithShowsThrough()
        {
            TerrainTileMap map = CreateMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            MeshRenderer renderer = view.GetComponentInChildren<MeshRenderer>();
            Assert.That(renderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000),
                "An opaque queue would paint the cutout's transparent pixels over the ground below it.");
            Assert.That(renderer.sortingOrder, Is.EqualTo(TerrainChunkView.SortingOrder));
        }

        [Test]
        public void Rebuild_CoversTheWholeBlockAndFacesUpwards()
        {
            TerrainTileMap map = CreateMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Mesh mesh = view.GetComponentInChildren<MeshFilter>().sharedMesh;
            float blockSize = TileSize * ChunkSizeInTiles;
            Assert.That(mesh.bounds.size.x, Is.EqualTo(blockSize).Within(.001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(blockSize).Within(.001f));
            Assert.That(mesh.normals[0].y, Is.GreaterThan(.99f));
        }

        [Test]
        public void Rebuild_TakesUvsFromWorldSpaceSoNeighbouringTilesJoinWithoutASeam()
        {
            TerrainTileMap map = CreateMap(out TerrainSurfaceDefinition surface);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Mesh mesh = view.GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector2[] uv = mesh.uv;
            Vector3[] vertices = mesh.vertices;
            // UV tracks world position at a fixed rate rather than restarting per quad, which is what
            // makes the texture run on unbroken instead of showing a seam at every cell border.
            Assert.That(uv[0], Is.EqualTo(Vector2.zero));
            float span = vertices[1].x - vertices[0].x;
            Assert.That(uv[1].x - uv[0].x, Is.EqualTo(span / surface.TextureTileSize).Within(.0001f));
        }

        [Test]
        public void Rebuild_AfterATileIsClearedAway_LeavesAHoleExactlyOneTileWide()
        {
            TerrainTileMap map = CreateMap(out _);
            TerrainChunkView view = CreateView();
            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);
            float before = DrawnArea(view);

            map.Dig(new TerrainTileCoordinate(1, 1));
            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            // Digging works on whole tiles even though the outline is drawn more finely, so exactly one
            // tile's worth of terrain disappears.
            Assert.That(before - DrawnArea(view), Is.EqualTo(TileSize * TileSize).Within(.001f));
        }

        /// <summary>
        /// The outline follows the terrain field rather than the dig grid, so it has to be able to cut
        /// inside a tile. With every cell covered there is nothing to cut, so this uses a real field.
        /// </summary>
        [Test]
        public void Rebuild_DrawsAnOutlineFinerThanTheDigGrid()
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure("patchy", "Patchy", 1, 1f, string.Empty);
            _created.Add(surface);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(surface, 12f, .5f, 31));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(2024, settings);

            bool foundPartialTile = false;
            for (int chunkX = 0; chunkX < 12 && !foundPartialTile; chunkX++)
            {
                var origin = new TerrainTileCoordinate(chunkX * ChunkSizeInTiles, 0);
                TerrainChunkView view = CreateView();
                view.Rebuild(map, origin, ChunkSizeInTiles);
                float area = DrawnArea(view);
                // An outline locked to the dig grid could only ever draw whole tiles.
                if (area > .001f && Mathf.Abs(area / (TileSize * TileSize) - Mathf.Round(area / (TileSize * TileSize))) > .01f)
                {
                    foundPartialTile = true;
                }
            }

            Assert.That(foundPartialTile, Is.True,
                "Every block drew whole tiles only, so patch edges would still be three-metre steps.");
        }

        /// <summary>Total ground area the block draws, summed over its triangles.</summary>
        private static float DrawnArea(TerrainChunkView view)
        {
            float area = 0f;
            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    area += Vector3.Cross(b - a, c - a).magnitude * .5f;
                }
            }

            return area;
        }

        [Test]
        public void Rebuild_OnBaseGroundOnly_DrawsNothing()
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure("unreachable", "Unreachable", 1, 1f, string.Empty);
            _created.Add(surface);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            // Threshold one: the layer never reaches, so the block is bare regolith.
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1, new TerrainPatchLayer(surface, 20f, 1f, 0));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(99, settings);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Assert.That(view.LayerMeshCount, Is.Zero);
            Assert.That(view.GetComponentInChildren<MeshFilter>(), Is.Null);
        }

        private TerrainChunkView CreateView()
        {
            var root = new GameObject("Terrain Chunk");
            _created.Add(root);
            return root.AddComponent<TerrainChunkView>();
        }

        private TerrainTileMap CreateMap(out TerrainSurfaceDefinition surface)
        {
            surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.name = "Test Terrain";
            surface.Configure("test_terrain", "Test Terrain", 1, 1f, string.Empty);
            surface.ConfigureTexture(null, 8f);
            _created.Add(surface);

            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1, new TerrainPatchLayer(surface, 20f, 0f, 0));
            _created.Add(settings);

            var map = new TerrainTileMap();
            map.Configure(99, settings);
            return map;
        }
    }
}
