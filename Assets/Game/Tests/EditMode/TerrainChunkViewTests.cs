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
        public void Rebuild_DrawsOneQuadForTheWholeChunk()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(view.LayerMeshCount, Is.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(6));
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
        public void Rebuild_PreservesLegacyTextureScaleOnTheSharedMaterial()
        {
            TerrainTileMap map = CreateCoveringMap(out TerrainSurfaceDefinition surface);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Material material = view.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.GetFloat("_Layer0Scale"),
                Is.EqualTo(1f / surface.TextureTileSize).Within(.0001f));
        }

        [Test]
        public void Rebuild_ConfiguresOneCompleteArtworkPerGameplayTile()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            var properties = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(Shader.PropertyToID("_TilesPerChunk")),
                Is.EqualTo(ChunkSizeInTiles));
        }

        [Test]
        public void Rebuild_BindsStableTextureVariantsForTheConfiguredSurface()
        {
            TerrainTileMap map = CreateCoveringMap(out TerrainSurfaceDefinition surface);
            Texture2D primary = CreateTexture("Primary");
            Texture2D second = CreateTexture("Second");
            Texture2D third = CreateTexture("Third");
            surface.ConfigureTextureVariants(8f, primary, second, third);
            TerrainChunkView view = CreateView();
            var origin = new TerrainTileCoordinate(-4, 8);

            view.Rebuild(map, origin, ChunkSizeInTiles);

            Material material = view.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.GetFloat("_VariantLayerIndex"), Is.EqualTo(0f));
            Assert.That(material.GetFloat("_VariantCount"), Is.EqualTo(3f));
            Assert.That(material.GetTexture("_Layer0"), Is.EqualTo(primary));
            Assert.That(material.GetTexture("_Variant1"), Is.EqualTo(second));
            Assert.That(material.GetTexture("_Variant2"), Is.EqualTo(third));

            var properties = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            Vector4 tileOrigin = properties.GetVector(Shader.PropertyToID("_TileOrigin"));
            Assert.That(tileOrigin.x, Is.EqualTo(origin.X));
            Assert.That(tileOrigin.y, Is.EqualTo(origin.Z));
            Assert.That(properties.GetFloat(Shader.PropertyToID("_VariantSeed")),
                Is.EqualTo(map.WorldSeed));
        }

        [Test]
        public void Rebuild_BindsIndependentTextureVariantsForTwoSurfaces()
        {
            TerrainSurfaceDefinition upper = CreateSurface("upper", 8f);
            TerrainSurfaceDefinition lower = CreateSurface("lower", 8f);
            Texture2D upperPrimary = CreateTexture("Upper Primary");
            Texture2D upperSecond = CreateTexture("Upper Second");
            Texture2D upperThird = CreateTexture("Upper Third");
            Texture2D lowerPrimary = CreateTexture("Lower Primary");
            Texture2D lowerSecond = CreateTexture("Lower Second");
            Texture2D lowerThird = CreateTexture("Lower Third");
            upper.ConfigureTextureVariants(8f, upperPrimary, upperSecond, upperThird);
            lower.ConfigureTextureVariants(8f, lowerPrimary, lowerSecond, lowerThird);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, ChunkSizeInTiles, 1,
                new TerrainPatchLayer(upper, 12f, .5f, 31),
                new TerrainPatchLayer(lower, 20f, 1f, 91));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(2024, settings);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Material material = view.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.GetFloat("_VariantLayerIndex"), Is.EqualTo(0f));
            Assert.That(material.GetTexture("_Variant1"), Is.EqualTo(upperSecond));
            Assert.That(material.GetTexture("_Variant2"), Is.EqualTo(upperThird));
            Assert.That(material.GetFloat("_SecondVariantLayerIndex"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_SecondVariantCount"), Is.EqualTo(3f));
            Assert.That(material.GetTexture("_SecondVariant1"), Is.EqualTo(lowerSecond));
            Assert.That(material.GetTexture("_SecondVariant2"), Is.EqualTo(lowerThird));
        }

        [Test]
        public void Rebuild_CreatesBilinearlyFilteredControlMapsWithAGutter()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();

            view.Rebuild(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles);

            Texture2D control = GetControlTexture(view, "_Control0");
            int expectedSize = TerrainControlMapBuilder.TextureSizeFor(
                TerrainChunkRenderResources.DefaultControlMapResolution);
            Assert.That(control.width, Is.EqualTo(expectedSize));
            Assert.That(control.height, Is.EqualTo(expectedSize));
            Assert.That(control.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(control.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void ControlMaps_MatchAtNeighbouringChunkEdges()
        {
            TerrainTileMap map = CreatePatchyMap();
            int textureSize = TerrainControlMapBuilder.TextureSizeFor(TestResolution);
            var left0 = new Color32[textureSize * textureSize];
            var left1 = new Color32[textureSize * textureSize];
            var right0 = new Color32[textureSize * textureSize];
            var right1 = new Color32[textureSize * textureSize];
            TerrainControlMapBuilder.Fill(map, new TerrainTileCoordinate(0, 0), ChunkSizeInTiles,
                TestResolution, 1.2f, left0, left1);
            TerrainControlMapBuilder.Fill(map,
                new TerrainTileCoordinate(ChunkSizeInTiles, 0), ChunkSizeInTiles,
                TestResolution, 1.2f, right0, right1);

            int leftX = TerrainControlMapBuilder.GutterSize + TestResolution;
            int rightX = TerrainControlMapBuilder.GutterSize;
            for (int z = TerrainControlMapBuilder.GutterSize;
                 z <= TerrainControlMapBuilder.GutterSize + TestResolution; z++)
            {
                Assert.That(left0[z * textureSize + leftX], Is.EqualTo(right0[z * textureSize + rightX]));
                Assert.That(left1[z * textureSize + leftX], Is.EqualTo(right1[z * textureSize + rightX]));
            }
        }

        [Test]
        public void ControlMap_ContainsContinuousWeightsAcrossAPatchRim()
        {
            TerrainTileMap map = CreatePatchyMap();
            Color32[] control = BuildFirstControlMap(map, new TerrainTileCoordinate(0, 0));
            bool foundTransition = false;
            for (int i = 0; i < control.Length; i++)
            {
                if (control[i].r > 0 && control[i].r < byte.MaxValue)
                {
                    foundTransition = true;
                    break;
                }
            }

            Assert.That(foundTransition, Is.True,
                "The patch edge must be a continuous weight band rather than a binary tile outline.");
        }

        [Test]
        public void ControlMap_OverlappingLayers_OnlyGiveWeightToTheHighestPriorityLayer()
        {
            TerrainTileMap map = CreateOverlappingMap();
            Color32[] control = BuildFirstControlMap(map, new TerrainTileCoordinate(0, 0));
            bool foundUpperLayer = false;
            for (int i = 0; i < control.Length; i++)
            {
                if (control[i].r > 0)
                {
                    foundUpperLayer = true;
                    Assert.That(control[i].g, Is.Zero,
                        "A lower terrain layer must not render below transparent pixels of the winning layer.");
                }
            }

            Assert.That(foundUpperLayer, Is.True, "The test area did not reach the upper terrain layer.");
        }

        [Test]
        public void Rebuild_AfterATileIsCleared_ZerosItsControlWeightOnly()
        {
            TerrainTileMap map = CreateCoveringMap(out _);
            TerrainChunkView view = CreateView();
            var origin = new TerrainTileCoordinate(0, 0);
            view.Rebuild(map, origin, ChunkSizeInTiles);
            Texture2D before = GetControlTexture(view, "_Control0");
            int clearedSample = PixelAtTileCenter(before.width, 1, 1);
            int neighbourSample = PixelAtTileCenter(before.width, 2, 1);
            Assert.That(before.GetPixels32()[clearedSample].r, Is.EqualTo(byte.MaxValue));

            map.Dig(new TerrainTileCoordinate(1, 1));
            view.Rebuild(map, origin, ChunkSizeInTiles);
            Color32[] after = GetControlTexture(view, "_Control0").GetPixels32();

            Assert.That(after[clearedSample].r, Is.Zero);
            Assert.That(after[neighbourSample].r, Is.EqualTo(byte.MaxValue));
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

        private Color32[] BuildFirstControlMap(TerrainTileMap map, TerrainTileCoordinate origin)
        {
            int textureSize = TerrainControlMapBuilder.TextureSizeFor(TestResolution);
            var control0 = new Color32[textureSize * textureSize];
            var control1 = new Color32[textureSize * textureSize];
            TerrainControlMapBuilder.Fill(map, origin, ChunkSizeInTiles, TestResolution, 1.2f,
                control0, control1);
            return control0;
        }

        private static int PixelAtTileCenter(int textureSize, int tileX, int tileZ)
        {
            int samplesPerTile = TerrainChunkRenderResources.DefaultControlMapResolution / ChunkSizeInTiles;
            int x = TerrainControlMapBuilder.GutterSize + tileX * samplesPerTile + samplesPerTile / 2;
            int z = TerrainControlMapBuilder.GutterSize + tileZ * samplesPerTile + samplesPerTile / 2;
            return z * textureSize + x;
        }

        private static Texture2D GetControlTexture(TerrainChunkView view, string propertyName)
        {
            var properties = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            return properties.GetTexture(propertyName) as Texture2D;
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
