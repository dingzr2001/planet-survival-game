using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainChunkViewTests
    {
        private const float TileSize = 3f;
        private const int ChunkSizeInTiles = 4;
        private const int TestResolution = 32;

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
        public void Rebuild_DrawsOneGridForTheWholeChunk()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(view.LayerMeshCount, Is.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
            // A fine grid, so the ground can be drawn sunk into craters.
            Assert.That(mesh.vertexCount, Is.GreaterThan(100));
            Assert.That(mesh.bounds.min.y, Is.LessThan(-10f), "Sunken ground must not be culled.");
            Vector3 extent = mesh.bounds.size;
            Assert.That(extent.x, Is.EqualTo(TileSize * ChunkSizeInTiles).Within(.001f));
        }

        [Test]
        public void Rebuild_UsesTheTerrainBlendShaderAndExpectedSorting()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            MeshRenderer renderer = view.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterial.shader.name,
                Is.EqualTo(TerrainChunkRenderResources.ShaderName));
            Assert.That(renderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000));
            Assert.That(renderer.sortingOrder, Is.EqualTo(TerrainChunkView.SortingOrder));
        }

        [Test]
        public void Rebuild_CoversTheWholeBlockAndFacesUpwards()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
            float blockSize = TileSize * ChunkSizeInTiles;
            Assert.That(mesh.bounds.size.x, Is.EqualTo(blockSize).Within(.001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(blockSize).Within(.001f));
            Assert.That(mesh.normals[0].y, Is.GreaterThan(.99f));
        }

        [Test]
        public void Resources_GiveEveryLayerVariantItsOwnArtworkSlice()
        {
            TerrainSurfaceDefinition upper = CreateSurface("upper", 8f);
            TerrainSurfaceDefinition lower = CreateSurface("lower", 8f);
            upper.ConfigureTextureVariants(8f, CreateTexture("Upper A"), CreateTexture("Upper B"),
                CreateTexture("Upper C"));
            lower.ConfigureTextureVariants(8f, CreateTexture("Lower A"));
            var layers = new[]
            {
                new TerrainPatchLayer(upper, 12f, .5f, 31),
                new TerrainPatchLayer(lower, 20f, 1f, 91)
            };

            using var resources = new TerrainChunkRenderResources(layers, TileSize * ChunkSizeInTiles);

            Assert.That(resources.SliceRangeOf(0), Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(resources.SliceRangeOf(1), Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(resources.PatchSliceCount, Is.EqualTo(4));
        }

        [Test]
        public void Resources_AppendFallenRocksAfterThePatchLayers()
        {
            TerrainSurfaceDefinition surface = CreateSurface("upper", 8f);
            surface.ConfigureTextureVariants(8f, CreateTexture("A"), CreateTexture("B"));
            var appearance = new LandformAppearance();
            appearance.ConfigureTalusStones(CreateTexture("Rock 1"), null, CreateTexture("Rock 2"));

            using var resources = new TerrainChunkRenderResources(
                new[] { new TerrainPatchLayer(surface, 12f, .5f, 31) }, TileSize * ChunkSizeInTiles,
                appearance: appearance);

            Assert.That(resources.TalusFirstSlice, Is.EqualTo(2));
            Assert.That(resources.TalusSliceCount, Is.EqualTo(2), "Missing rock textures are skipped.");
            Assert.That(resources.PatchSliceCount, Is.EqualTo(4));
        }

        [Test]
        public void Rebuild_BindsTheChunkPlacementAndSeed()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();
            var origin = new TerrainTileCoordinate(-4, 8);

            view.Rebuild(map, origin, ChunkSizeInTiles);

            var properties = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(Shader.PropertyToID("_TilesPerChunk")), Is.EqualTo(ChunkSizeInTiles));
            Vector4 tileOrigin = properties.GetVector(Shader.PropertyToID("_TileOrigin"));
            Assert.That(tileOrigin.x, Is.EqualTo(origin.X));
            Assert.That(tileOrigin.y, Is.EqualTo(origin.Z));
            Vector4 chunkOrigin = properties.GetVector(Shader.PropertyToID("_ChunkOrigin"));
            Assert.That(chunkOrigin.x, Is.EqualTo(origin.X * TileSize).Within(.0001f));
            Assert.That(chunkOrigin.y, Is.EqualTo(origin.Z * TileSize).Within(.0001f));
            Assert.That(properties.GetFloat(Shader.PropertyToID("_VariantSeed")), Is.EqualTo(map.WorldSeed));
        }

        [Test]
        public void Rebuild_CreatesAPointFilteredPatchMapWithAOneTileGutter()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Texture2D patches = view.PatchTexture;
            Assert.That(patches.width, Is.EqualTo(ChunkSizeInTiles + 2));
            Assert.That(patches.filterMode, Is.EqualTo(FilterMode.Point),
                "Blending two layer numbers would name a third layer.");
        }

        [Test]
        public void Rebuild_WithLandforms_BindsAHalfPrecisionLandformMapAndDraws()
        {
            TerrainTileMap map = CreateLandformMap();
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Texture2D elevation = view.ElevationTexture;
            Assert.That(elevation.format, Is.EqualTo(TextureFormat.RGBAHalf));
            Assert.That(elevation.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(view.LayerMeshCount, Is.EqualTo(1),
                "Relief shading covers every chunk once landforms exist, even without patches.");
        }

        [Test]
        public void LandformMaps_MatchAtNeighbouringChunkEdges()
        {
            TerrainTileMap map = CreateLandformMap();
            var appearance = new LandformAppearance();
            float chunkSize = TileSize * ChunkSizeInTiles;
            int gutter = 3;
            int textureSize = TerrainControlMapBuilder.ElevationTextureSizeFor(TestResolution, gutter);
            int count = textureSize * textureSize;
            var left = new ushort[count * TerrainControlMapBuilder.LandformChannels];
            var right = new ushort[count * TerrainControlMapBuilder.LandformChannels];
            TerrainControlMapBuilder.FillLandformMaps(map, 0f, 0f, chunkSize, TestResolution, gutter, appearance,
                new float[count], new float[count], new float[count], left,
                new ushort[count * TerrainControlMapBuilder.VolcanicChannels], out _);
            TerrainControlMapBuilder.FillLandformMaps(map, chunkSize, 0f, chunkSize, TestResolution, gutter, appearance,
                new float[count], new float[count], new float[count], right,
                new ushort[count * TerrainControlMapBuilder.VolcanicChannels], out _);

            for (int z = 0; z < textureSize; z++)
            {
                int leftTexel = (z * textureSize + gutter + TestResolution) * TerrainControlMapBuilder.LandformChannels;
                int rightTexel = (z * textureSize + gutter) * TerrainControlMapBuilder.LandformChannels;
                for (int channel = 0; channel < TerrainControlMapBuilder.LandformChannels; channel++)
                {
                    Assert.That(left[leftTexel + channel], Is.EqualTo(right[rightTexel + channel]),
                        $"Channel {channel} differs on the shared edge at row {z}.");
                }
            }
        }

        [Test]
        public void Rebuild_OnFlatGround_BuildsNoMountainMesh()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Assert.That(view.MountainMesh, Is.Null);
            Assert.That(view.FallenRockCount, Is.Zero);
        }

        [Test]
        public void Rebuild_WhereAMountainStands_BuildsItsMeshTallerThanItsCliffs()
        {
            TerrainTileMap map = CreateLandformMap();
            var appearance = new LandformAppearance();
            TerrainTileCoordinate origin = FindMountainChunk(map);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, origin, ChunkSizeInTiles);

            Mesh mountain = view.MountainMesh;
            Assert.That(mountain, Is.Not.Null);
            Assert.That(mountain.vertexCount, Is.GreaterThan(0));
            // Built exaggerated so walls read at sprite scale under the steep camera.
            Assert.That(mountain.bounds.max.y,
                Is.GreaterThan(appearance.CliffHeight * (1f - appearance.CliffHeightVariation) * 2f));
            Assert.That(mountain.bounds.min.y, Is.LessThan(0f), "The foot is buried so it emerges from the ground.");
        }

        [Test]
        public void PatchMaps_MatchAtNeighbouringChunkEdges()
        {
            TerrainTileMap map = CreatePatchyMap();
            int textureSize = TerrainControlMapBuilder.PatchTextureSizeFor(ChunkSizeInTiles);
            var left = new Color32[textureSize * textureSize];
            var right = new Color32[textureSize * textureSize];
            TerrainControlMapBuilder.FillPatchIds(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles, 1.2f, left);
            TerrainControlMapBuilder.FillPatchIds(map, new TerrainTileCoordinate(ChunkSizeInTiles, 0),
                ChunkSizeInTiles, 1.2f, right);

            for (int z = 0; z < textureSize; z++)
            {
                // The left chunk's gutter column is the right chunk's first tile, and vice versa.
                Assert.That(left[z * textureSize + textureSize - 1], Is.EqualTo(right[z * textureSize + 1]));
                Assert.That(left[z * textureSize + textureSize - 2], Is.EqualTo(right[z * textureSize]));
            }
        }

        [Test]
        public void PatchMap_ThinsArtworkTowardsAPatchRim()
        {
            TerrainTileMap map = CreatePatchyMap();
            Color32[] patches = BuildPatchMap(map, new TerrainTileCoordinate(0, 0), 16);
            bool foundRim = false;
            for (int i = 0; i < patches.Length; i++)
            {
                foundRim |= patches[i].r > 0 && patches[i].g < byte.MaxValue;
            }

            Assert.That(foundRim, Is.True, "Tiles near a patch edge must carry less than full depth.");
        }

        [Test]
        public void PatchMap_NamesExactlyTheLayerGameplayAssignsToEachTile()
        {
            TerrainTileMap map = CreateOverlappingMap();
            const int tiles = 12;
            Color32[] patches = BuildPatchMap(map, new TerrainTileCoordinate(0, 0), tiles);
            int size = TerrainControlMapBuilder.PatchTextureSizeFor(tiles);
            bool foundUpperLayer = false;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var tile = new TerrainTileCoordinate(x - TerrainControlMapBuilder.PatchGutter,
                        z - TerrainControlMapBuilder.PatchGutter);
                    int expected = map.GetLayerIndex(tile) + 1;
                    Assert.That(patches[z * size + x].r, Is.EqualTo(expected),
                        "Art over a tile must always be what digging it yields.");
                    foundUpperLayer |= expected == 1;
                }
            }

            Assert.That(foundUpperLayer, Is.True, "The test area did not reach the upper terrain layer.");
        }

        [Test]
        public void Rebuild_AfterATileIsCleared_MarksOnlyThatTileDug()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();
            var origin = new TerrainTileCoordinate(0, 0);
            view.Rebuild(map, origin, ChunkSizeInTiles);
            int size = view.PatchTexture.width;
            int cleared = TexelOf(size, 1, 1);
            int neighbour = TexelOf(size, 2, 1);
            Assert.That(view.PatchTexture.GetPixels32()[cleared].r, Is.EqualTo(1));

            map.Dig(new TerrainTileCoordinate(1, 1));
            view.Rebuild(map, origin, ChunkSizeInTiles);
            Color32[] after = view.PatchTexture.GetPixels32();

            Assert.That(after[cleared].r, Is.Zero);
            Assert.That(after[cleared].b, Is.EqualTo(TerrainControlMapBuilder.ClearedFlag));
            Assert.That(after[neighbour].r, Is.EqualTo(1));
            Assert.That(after[neighbour].b, Is.Zero);
        }

        [Test]
        public void Rebuild_OnBaseGroundOnly_DisablesTheOverlayDraw()
        {
            var surface = CreateSurface("unreachable", 8f);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(surface, 20f, 0f, 0));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(99, settings);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Assert.That(view.LayerMeshCount, Is.Zero);
            Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        private static Color32[] BuildPatchMap(TerrainTileMap map, TerrainTileCoordinate origin, int tiles)
        {
            int size = TerrainControlMapBuilder.PatchTextureSizeFor(tiles);
            var pixels = new Color32[size * size];
            TerrainControlMapBuilder.FillPatchIds(map, origin, tiles, 1.2f, pixels);
            return pixels;
        }

        private static int TexelOf(int textureSize, int tileX, int tileZ)
        {
            return (tileZ + TerrainControlMapBuilder.PatchGutter) * textureSize + tileX + TerrainControlMapBuilder.PatchGutter;
        }

        private TerrainChunkView CreateView()
        {
            var root = new GameObject("Terrain Chunk");
            _created.Add(root);
            return root.AddComponent<TerrainChunkView>();
        }

        private Texture2D CreateTexture(string textureName)
        {
            var texture = new Texture2D(2, 2) { name = textureName };
            _created.Add(texture);
            return texture;
        }

        private TerrainTileMap CreateCoveringMap(out TerrainSurfaceDefinition surface)
        {
            surface = CreateSurface("test_terrain", 8f);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(surface, 20f, 1f, 0));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(99, settings);
            return map;
        }

        private TerrainTileMap CreateLandformMap()
        {
            var landforms = ScriptableObject.CreateInstance<LandformSettings>();
            _created.Add(landforms);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1);
            settings.ConfigureLandforms(landforms, null);
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(2024, settings, 1f, Vector2.zero);
            return map;
        }

        /// <summary>A chunk origin, on the chunk lattice, whose chunk holds mountain ground.</summary>
        private static TerrainTileCoordinate FindMountainChunk(TerrainTileMap map)
        {
            float chunkSize = TileSize * ChunkSizeInTiles;
            for (int radius = 30; radius < 1500; radius += 6)
            {
                for (int step = 0; step < 24; step++)
                {
                    float angle = step * Mathf.PI / 12f;
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    if (!map.IsBlockedAt(x, z))
                    {
                        continue;
                    }

                    int chunkX = Mathf.FloorToInt(x / chunkSize);
                    int chunkZ = Mathf.FloorToInt(z / chunkSize);
                    return new TerrainTileCoordinate(chunkX * ChunkSizeInTiles, chunkZ * ChunkSizeInTiles);
                }
            }

            Assert.Fail("No mountain was found near the origin.");
            return default;
        }

        private TerrainTileMap CreatePatchyMap()
        {
            TerrainSurfaceDefinition surface = CreateSurface("patchy", 8f);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(surface, 12f, .5f, 31));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(2024, settings);
            return map;
        }

        private TerrainTileMap CreateOverlappingMap()
        {
            TerrainSurfaceDefinition upper = CreateSurface("upper", 8f);
            TerrainSurfaceDefinition lower = CreateSurface("lower", 8f);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(upper, 12f, .5f, 31),
                new TerrainPatchLayer(lower, 20f, 1f, 91));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(2024, settings);
            return map;
        }

        private TerrainSurfaceDefinition CreateSurface(string terrainId, float textureTileSize)
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.name = terrainId;
            surface.Configure(terrainId, terrainId, 1, 1f, string.Empty);
            surface.ConfigureTexture(null, textureTileSize);
            _created.Add(surface);
            return surface;
        }
    }
}
