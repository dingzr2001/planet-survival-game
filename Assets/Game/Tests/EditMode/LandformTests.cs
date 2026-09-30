using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Definitions;
using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class LandformTests
    {
        private const int WorldSeed = 13359;
        private static readonly Vector2 LandingSite = new(12f, 12f);

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
        public void Sampler_GivesTheSameAnswerWhateverOrderPointsAreAskedIn()
        {
            LandformSettings settings = CreateSettings();
            var forward = new LandformSampler(WorldSeed, settings, LandingSite);
            var backward = new LandformSampler(WorldSeed, settings, LandingSite);
            var points = new List<Vector2>();
            for (int i = 0; i < 200; i++)
            {
                points.Add(new Vector2(i * 37.3f - 3000f, i * -23.9f + 1500f));
            }

            var forwardResults = new List<LandformSample>();
            for (int i = 0; i < points.Count; i++)
            {
                forwardResults.Add(forward.Sample(points[i].x, points[i].y));
            }

            for (int i = points.Count - 1; i >= 0; i--)
            {
                LandformSample sample = backward.Sample(points[i].x, points[i].y);
                Assert.That(sample.Elevation, Is.EqualTo(forwardResults[i].Elevation));
                Assert.That(sample.Kind, Is.EqualTo(forwardResults[i].Kind));
            }
        }

        [Test]
        public void Sampler_DifferentSeedsProduceDifferentWorlds()
        {
            LandformSettings settings = CreateSettings();
            var first = new LandformSampler(WorldSeed, settings, LandingSite);
            var second = new LandformSampler(WorldSeed + 1, settings, LandingSite);
            int differing = 0;
            for (int i = 0; i < 100; i++)
            {
                float x = 200f + i * 31f;
                differing += Mathf.Approximately(first.Sample(x, -400f).Elevation, second.Sample(x, -400f).Elevation)
                    ? 0
                    : 1;
            }

            Assert.That(differing, Is.GreaterThan(90));
        }

        [TestCase(13359)]
        [TestCase(1)]
        [TestCase(2)]
        public void LandingSite_IsOpenGroundWithAStarterLakeNearby(int seed)
        {
            LandformSettings settings = CreateSettings();
            var sampler = new LandformSampler(seed, settings, LandingSite);
            float lakeReach = settings.StarterLakeRadius * 1.4f;
            float clearRadius = settings.StarterLakeDistance - lakeReach - 1f;

            for (float z = -settings.StartFlatRadius; z <= settings.StartFlatRadius; z += 1.5f)
            {
                for (float x = -settings.StartFlatRadius; x <= settings.StartFlatRadius; x += 1.5f)
                {
                    float distance = Mathf.Sqrt(x * x + z * z);
                    if (distance > settings.StartFlatRadius)
                    {
                        continue;
                    }

                    LandformKind kind = sampler.KindAt(LandingSite.x + x, LandingSite.y + z);
                    Assert.That(kind, Is.Not.EqualTo(LandformKind.Mountain),
                        $"A mountain {distance:0.#} m from the landing site would wall the player in.");
                    if (distance < clearRadius)
                    {
                        Assert.That(kind, Is.EqualTo(LandformKind.Plain), $"Ground {distance:0.#} m from the pod.");
                    }
                }
            }

            Vector2 lake = sampler.StarterLakeCenter;
            Assert.That(Vector2.Distance(lake, LandingSite), Is.EqualTo(settings.StarterLakeDistance).Within(.01f));
            Assert.That(sampler.KindAt(lake.x, lake.y), Is.EqualTo(LandformKind.IceLake));
        }

        [Test]
        public void Craters_HaveFloorsBelowTheirRims_AndBlockingRimsHaveWalkableGaps()
        {
            LandformSettings settings = CreateSettings();
            settings.ConfigureCraters(1f, .5f);
            var sampler = new LandformSampler(WorldSeed, settings, LandingSite);
            bool checkedBlockingRim = false;
            int checkedCraters = 0;
            for (int region = 3; region < 40 && checkedCraters < 6; region++)
            {
                if (!sampler.Craters.TryGetCrater(region, -region, out Crater crater))
                {
                    continue;
                }

                checkedCraters++;
                float floor = sampler.Sample(crater.CenterX, crater.CenterZ).Elevation;
                int blocked = 0;
                int open = 0;
                float highestRim = float.NegativeInfinity;
                for (int step = 0; step < 72; step++)
                {
                    float angle = step * Mathf.PI * 2f / 72f;
                    float x = crater.CenterX + Mathf.Cos(angle) * crater.Radius;
                    float z = crater.CenterZ + Mathf.Sin(angle) * crater.Radius;
                    LandformSample rim = sampler.Sample(x, z);
                    highestRim = Mathf.Max(highestRim, rim.Elevation);
                    blocked += rim.Kind == LandformKind.Mountain ? 1 : 0;
                    open += rim.Kind == LandformKind.Mountain ? 0 : 1;
                }

                Assert.That(floor, Is.LessThan(highestRim), $"Crater at ({crater.CenterX:0}, {crater.CenterZ:0}).");
                if (crater.BlocksMovement)
                {
                    Assert.That(blocked, Is.GreaterThan(0), "A blocking rim must rise into mountain.");
                    Assert.That(open, Is.GreaterThan(0), "A blocking rim must leave a way into the crater.");
                    checkedBlockingRim = true;
                }
            }

            Assert.That(checkedCraters, Is.GreaterThan(0), "No crater was generated in the sampled regions.");
            Assert.That(checkedBlockingRim, Is.True, "No blocking crater was generated in the sampled regions.");
        }

        [Test]
        public void Coverage_StaysNearTheAuthoredShares()
        {
            LandformSettings settings = CreateSettings();
            // Rock ground takes ice away wherever it lies (tested on its own); this measures the relief alone.
            settings.ConfigureRockGround(0f, 0f, settings.RockGroundClearance);
            var sampler = new LandformSampler(WorldSeed, settings, LandingSite);
            var counts = new int[System.Enum.GetValues(typeof(LandformKind)).Length];
            // Smoothed blocks are built lazily, so sample an area that stays within a few hundred of them.
            const int samplesPerAxis = 100;
            const float spacing = 8f;
            for (int z = 0; z < samplesPerAxis; z++)
            {
                for (int x = 0; x < samplesPerAxis; x++)
                {
                    counts[(int)sampler.KindAt(1000f + x * spacing, -2000f + z * spacing)]++;
                }
            }

            float total = samplesPerAxis * samplesPerAxis;
            float mountain = counts[(int)LandformKind.Mountain] / total;
            float lake = counts[(int)LandformKind.IceLake] / total;
            float lowGround = (counts[(int)LandformKind.Basin] + counts[(int)LandformKind.IceLake]) / total;
            Assert.That(mountain, Is.InRange(.02f, .2f), "Mountain share");
            Assert.That(lake, Is.InRange(.01f, .12f), "Ice-lake share");
            Assert.That(lowGround, Is.InRange(.06f, .32f), "Basin share including lakes");
        }

        [TestCase(13359)]
        [TestCase(1)]
        [TestCase(2)]
        public void OpenGroundAroundTheLandingSite_IsReachableOnFoot(int seed)
        {
            var sampler = new LandformSampler(seed, CreateSettings(), LandingSite);

            float reachable = LandformReachability.ReachableOpenShare(
                (x, z) => sampler.KindAt(x, z) == LandformKind.Mountain, LandingSite, 400);

            Assert.That(reachable, Is.GreaterThanOrEqualTo(.95f),
                "Passes and crater gaps must keep mountains from walling off open ground.");
        }

        [TestCase(13359)]
        [TestCase(1)]
        [TestCase(2)]
        public void MountainOutlines_HaveAlmostNoThinTaperingSlivers(int seed)
        {
            float shaped = SliverShare(new LandformSampler(seed, CreateSettings(), LandingSite));

            // What remains are the rounded ends of narrow massifs, which the metric cannot tell from tips.
            Assert.That(shaped, Is.LessThan(.03f),
                "Wedge-shaped tongues of mountain should be smoothed away, not left as spikes.");
        }

        [Test]
        public void OutlineShaping_RemovesMostOfTheRawSlivers()
        {
            LandformSettings raw = CreateSettings();
            raw.ConfigureOutlineSmoothing(0f, 0f);
            float rawShare = SliverShare(new LandformSampler(WorldSeed, raw, LandingSite));
            float shaped = SliverShare(new LandformSampler(WorldSeed, CreateSettings(), LandingSite));

            Assert.That(shaped, Is.LessThan(rawShare * .4f), $"raw {rawShare:P2}, shaped {shaped:P2}");
        }

        [Test]
        public void BlockedCellRectangles_CoverEveryBlockedCellExactlyOnce()
        {
            const int width = 23;
            const int height = 17;
            var random = new System.Random(7);
            var mask = new bool[width * height];
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = random.NextDouble() < .45;
            }

            var rectangles = new List<RectInt>();
            BlockedCellRectangles.Merge(mask, width, height, rectangles);

            var coverage = new int[width * height];
            foreach (RectInt rectangle in rectangles)
            {
                for (int y = rectangle.yMin; y < rectangle.yMax; y++)
                {
                    for (int x = rectangle.xMin; x < rectangle.xMax; x++)
                    {
                        coverage[y * width + x]++;
                    }
                }
            }

            for (int i = 0; i < mask.Length; i++)
            {
                Assert.That(coverage[i], Is.EqualTo(mask[i] ? 1 : 0), $"Cell {i % width}, {i / width}");
            }

            Assert.That(rectangles.Count, Is.LessThan(CountTrue(mask)), "Merging must beat one box per cell.");
        }

        [Test]
        public void ChunkObstacles_CoverExactlyTheMountainCells()
        {
            TerrainTileMap map = CreateMap(null, null);
            Vector2Int mountainCell = FindCell(map.Landforms, LandformKind.Mountain);
            const float chunkSize = 22f;
            float originX = Mathf.Floor(mountainCell.x / chunkSize) * chunkSize;
            float originZ = Mathf.Floor(mountainCell.y / chunkSize) * chunkSize;
            var root = new GameObject("Chunk");
            _created.Add(root);

            TerrainChunkObstacles obstacles = root.AddComponent<TerrainChunkObstacles>();
            obstacles.Build(map, originX, originZ, chunkSize);

            int blockedCells = 0;
            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    blockedCells += map.IsBlockedAt(originX + x + .5f, originZ + z + .5f) ? 1 : 0;
                }
            }

            float colliderArea = 0f;
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>())
            {
                colliderArea += box.size.x * box.size.z;
                Assert.That(box.isTrigger, Is.False);
                Assert.That(map.IsBlockedAt(box.transform.position.x, box.transform.position.z) ||
                            box.size.x * box.size.z > 1f, Is.True);
            }

            Assert.That(blockedCells, Is.GreaterThan(0));
            Assert.That(colliderArea, Is.EqualTo(blockedCells).Within(.001f));
            Assert.That(obstacles.ColliderCount, Is.LessThan(blockedCells));
        }

        [Test]
        public void TileMap_MountainsCannotBeDug_LakesCarryTheIceSurface_AndPatchesStayOnOpenGround()
        {
            TerrainSurfaceDefinition patch = CreateSurface("patch");
            TerrainSurfaceDefinition ice = CreateSurface("ice");
            TerrainTileMap map = CreateMap(patch, ice);
            Vector2Int mountain = FindCell(map.Landforms, LandformKind.Mountain);
            Vector2Int lake = FindCell(map.Landforms, LandformKind.IceLake);
            Vector2Int plain = FindCell(map.Landforms, LandformKind.Plain);

            TerrainTileCoordinate mountainTile = map.TileAt(new Vector3(mountain.x + .5f, 0f, mountain.y + .5f));
            Assert.That(map.GetSurface(mountainTile), Is.Null);
            Assert.That(map.IsDiggable(mountainTile), Is.False);
            Assert.That(map.GetLayerIndex(mountainTile), Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));

            TerrainTileCoordinate lakeTile = map.TileAt(new Vector3(lake.x + .5f, 0f, lake.y + .5f));
            Assert.That(map.GetSurface(lakeTile), Is.SameAs(ice));
            Assert.That(map.GetLayerIndex(lakeTile), Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));

            TerrainTileCoordinate plainTile = map.TileAt(new Vector3(plain.x + .5f, 0f, plain.y + .5f));
            Assert.That(map.GetSurface(plainTile), Is.SameAs(patch));
        }

        [Test]
        public void IceLakes_EndWhereCraterWallsBegin()
        {
            LandformSettings settings = CreateSettings();
            settings.ConfigureCraters(1f, 1f);
            var sampler = new LandformSampler(WorldSeed, settings, LandingSite);
            int checkedCraters = 0;
            bool sawIceFloor = false;
            for (int region = 3; region < 20 && checkedCraters < 4; region++)
            {
                if (!sampler.Craters.TryGetCrater(region, region, out Crater crater))
                {
                    continue;
                }

                checkedCraters++;
                sawIceFloor |= sampler.KindAt(crater.CenterX, crater.CenterZ) == LandformKind.IceLake;
                for (float relative = CraterField.WallStart + .02f; relative < CraterField.SlopeReach; relative += .05f)
                {
                    for (int step = 0; step < 24; step++)
                    {
                        float angle = step * Mathf.PI / 12f;
                        float x = crater.CenterX + Mathf.Cos(angle) * crater.Radius * relative;
                        float z = crater.CenterZ + Mathf.Sin(angle) * crater.Radius * relative;
                        if (!sampler.Craters.IsOnSlope(x, z))
                        {
                            continue;
                        }

                        Assert.That(sampler.KindAt(x, z), Is.Not.EqualTo(LandformKind.IceLake),
                            $"Ice on the slope of the crater at ({crater.CenterX:0}, {crater.CenterZ:0}).");
                        Assert.That(sampler.Sample(x, z).Kind, Is.Not.EqualTo(LandformKind.IceLake));
                    }
                }
            }

            Assert.That(checkedCraters, Is.GreaterThan(0));
            Assert.That(sawIceFloor, Is.True, "Ice-floored craters should still hold ice on their floors.");
        }

        [Test]
        public void SheetIcePatches_StayOffCraterSlopes_WhileScatteredPatchesMayLieThere()
        {
            TerrainSurfaceDefinition sheet = CreateSurface("sheet");
            sheet.ConfigurePatchRendering(TerrainPatchRendering.Continuous);
            TerrainSurfaceDefinition rubble = CreateSurface("rubble");
            TerrainTileMap sheetMap = CreateMap(sheet, null);
            TerrainTileMap rubbleMap = CreateMap(rubble, null);
            int slopeTiles = 0;
            int rubbleOnSlopes = 0;
            for (int region = -6; region <= 6; region++)
            {
                if (!sheetMap.Landforms.Craters.TryGetCrater(region, 2, out Crater crater))
                {
                    continue;
                }

                for (int step = 0; step < 36; step++)
                {
                    float angle = step * Mathf.PI / 18f;
                    float x = crater.CenterX + Mathf.Cos(angle) * crater.Radius * 1.1f;
                    float z = crater.CenterZ + Mathf.Sin(angle) * crater.Radius * 1.1f;
                    TerrainTileCoordinate tile = sheetMap.TileAt(new Vector3(x, 0f, z));
                    if (!sheetMap.Landforms.Craters.IsOnSlope(tile.CenterX(sheetMap.TileSize), tile.CenterZ(sheetMap.TileSize)) ||
                        sheetMap.GetLandformKind(x, z) == LandformKind.Mountain)
                    {
                        continue;
                    }

                    slopeTiles++;
                    Assert.That(sheetMap.GetSurface(tile), Is.Not.SameAs(sheet), "Sheet ice lay on a crater slope.");
                    rubbleOnSlopes += rubbleMap.GetSurface(tile) == rubble ? 1 : 0;
                }
            }

            Assert.That(slopeTiles, Is.GreaterThan(0), "No crater slope was sampled.");
            Assert.That(rubbleOnSlopes, Is.EqualTo(slopeTiles), "Scattered patches still cover crater slopes.");
        }

        [Test]
        public void RockGround_KeepsTheLandingSiteClear_ButCoversItsShareFurtherOut()
        {
            LandformSettings settings = CreateSettings();
            var sampler = new LandformSampler(WorldSeed, settings, LandingSite);
            for (float radius = 0f; radius <= settings.RockGroundClearance; radius += 10f)
            {
                for (int step = 0; step < 24; step++)
                {
                    float angle = step * Mathf.PI / 12f;
                    float x = LandingSite.x + Mathf.Cos(angle) * radius;
                    float z = LandingSite.y + Mathf.Sin(angle) * radius;
                    Assert.That(sampler.Volcanic.IsRockGround(x, z), Is.False, $"Rock ground {radius} m from the landing site.");
                    Assert.That(sampler.KindAt(x, z), Is.Not.EqualTo(LandformKind.LavaLake));
                }
            }

            int lava = 0;
            int rock = 0;
            float total = 0f;
            ForEachWideSample((x, z) =>
            {
                total++;
                rock += sampler.Volcanic.IsRockGround(x, z) ? 1 : 0;
                lava += sampler.Volcanic.IsLava(x, z) ? 1 : 0;
            });

            Assert.That(rock / total, Is.InRange(settings.RockGroundCoverage * .5f, settings.RockGroundCoverage * 1.6f),
                "Rock-ground share");
            Assert.That(lava / total, Is.InRange(settings.LavaLakeCoverage * .3f, settings.LavaLakeCoverage * 2.2f),
                "Lava-lake share");
        }

        [Test]
        public void Lava_OnlyLiesInsideRockGround()
        {
            // The volcanic field alone: classifying every point would build the smoothed relief across the
            // whole grid. LandformSampler turns lava into lava lakes only where this field says so.
            var volcanicField = new LandformSampler(WorldSeed, CreateSettings(), LandingSite).Volcanic;
            int lavaSamples = 0;
            int interiorLava = 0;
            ForEachWideSample((x, z) =>
            {
                volcanicField.Sample(x, z, out float rockGround, out float lava);
                if (rockGround <= 0f)
                {
                    Assert.That(lava, Is.LessThan(0f), $"Lava activity off rock ground at ({x}, {z}).");
                }

                if (lava <= 1f)
                {
                    return;
                }

                lavaSamples++;
                interiorLava += rockGround > 1f ? 1 : 0;
            });

            Assert.That(lavaSamples, Is.GreaterThan(0));
            Assert.That(interiorLava / (float)lavaSamples, Is.GreaterThan(.8f),
                "Lava should keep mostly to the inner part of rock ground, dying out across its rim.");
        }

        [Test]
        public void RockGround_FormsExpansesHundredsOfMetresAcross()
        {
            var sampler = new LandformSampler(WorldSeed, CreateSettings(), LandingSite);
            const float spacing = 10f;
            const float minimumRun = 300f;
            int rockSamples = 0;
            int inLongRuns = 0;
            for (int row = 0; row < 12; row++)
            {
                float z = -6000f + row * 1000f;
                int run = 0;
                for (int column = 0; column <= 1200; column++)
                {
                    bool rock = column < 1200 && sampler.Volcanic.IsRockGround(-6000f + column * spacing, z);
                    if (rock)
                    {
                        run++;
                        continue;
                    }

                    rockSamples += run;
                    inLongRuns += run * spacing >= minimumRun ? run : 0;
                    run = 0;
                }
            }

            Assert.That(rockSamples, Is.GreaterThan(0));
            Assert.That(inLongRuns / (float)rockSamples, Is.GreaterThan(.7f),
                $"Most rock ground should lie in stretches of at least {minimumRun} m.");
        }

        [Test]
        public void IceNeverLiesOnRockGround()
        {
            var sampler = new LandformSampler(WorldSeed, CreateSettings(), LandingSite);
            int rockSamples = 0;
            // Sparse: each classified point builds a block of smoothed relief around it.
            for (int z = 0; z < 48; z++)
            {
                for (int x = 0; x < 48; x++)
                {
                    float worldX = -4800f + x * 200f;
                    float worldZ = -4800f + z * 200f;
                    if (!sampler.Volcanic.IsRockGround(worldX, worldZ))
                    {
                        continue;
                    }

                    rockSamples++;
                    Assert.That(sampler.KindAt(worldX, worldZ), Is.Not.EqualTo(LandformKind.IceLake),
                        $"Ice on rock ground at ({worldX}, {worldZ}).");
                }
            }

            Assert.That(rockSamples, Is.GreaterThan(0));
        }

        /// <summary>A grid wide enough to cross several rock-ground expanses.</summary>
        private static void ForEachWideSample(System.Action<float, float> visit)
        {
            const int samplesPerAxis = 160;
            const float spacing = 60f;
            for (int z = 0; z < samplesPerAxis; z++)
            {
                for (int x = 0; x < samplesPerAxis; x++)
                {
                    visit(-4800f + x * spacing, -4800f + z * spacing);
                }
            }
        }

        [Test]
        public void TileMap_LavaLakesBlockWalkingAndBuilding_AndCannotBeDug()
        {
            TerrainSurfaceDefinition patch = CreateSurface("patch");
            TerrainSurfaceDefinition ice = CreateSurface("ice");
            TerrainTileMap map = CreateMap(patch, ice);
            Vector2Int lava = FindCell(map.Landforms, LandformKind.LavaLake);

            Assert.That(map.IsBlockedAt(lava.x + .5f, lava.y + .5f), Is.True);
            TerrainTileCoordinate lavaTile = map.TileAt(new Vector3(lava.x + .5f, 0f, lava.y + .5f));
            Assert.That(map.GetSurface(lavaTile), Is.Null, "Lava cannot be dug.");
            Assert.That(map.IsDiggable(lavaTile), Is.False);
            Assert.That(map.GetLayerIndex(lavaTile), Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex),
                "No patch floats on lava.");
        }

        [Test]
        public void Building_IsRefusedOnMountains_AndAllowedOnIceLakes()
        {
            ItemDefinition alloy = CreateItem("alloy");
            TerrainSurfaceDefinition ice = CreateSurface("ice");
            TerrainTileMap map = CreateMap(null, ice);
            var inventory = new InventoryModel(30, 20);
            inventory.Add(alloy, 20);
            var service = new BuildingService(inventory, new BuildGrid(), map);
            BuildableDefinition wall = CreateBuildable("wall", alloy, new Vector2Int(1, 1));
            BuildableDefinition iceDrill = CreateBuildable("ice_drill", alloy, new Vector2Int(3, 3));
            var drill = ScriptableObject.CreateInstance<MiningDrillDefinition>();
            drill.Configure("ice", "water ice", CreateItem("ice_chunk"), 1f, 20, 2f,
                electricityPerOre: 5f, electricityCapacity: 25f,
                petroleumItem: CreateItem("petroleum"), petroleumPerItem: 5f, petroleumPerOre: 1f,
                petroleumCapacity: 20f);
            _created.Add(drill);
            iceDrill.ConfigureMiningDrill(drill);

            Vector2Int mountain = FindCell(map.Landforms, LandformKind.Mountain);
            BuildResult onMountain = service.CanPlace(wall, new BuildFootprint(mountain, Vector2Int.one));
            Assert.That(onMountain.Failure, Is.EqualTo(BuildFailure.TerrainBlocked));

            Vector2Int lakeCorner = FindLakeSquare(map.Landforms, 3);
            BuildResult onLake = service.CanPlace(iceDrill, new BuildFootprint(lakeCorner, new Vector2Int(3, 3)));
            Assert.That(onLake.Succeeded, Is.True, onLake.Message);
        }

        [Test]
        public void OffGridItems_AreRefusedOnMountains()
        {
            ItemDefinition alloy = CreateItem("alloy");
            TerrainTileMap map = CreateMap(null, null);
            var inventory = new InventoryModel(30, 20);
            inventory.Add(alloy, 20);
            var service = new BuildingService(inventory, new BuildGrid(), map);
            BuildableDefinition candle = CreateBuildable("candle", alloy, Vector2Int.one);
            candle.ConfigurePlacement(BuildPlacement.OffGrid, new Vector2(.6f, .6f));
            Vector2Int mountain = FindCell(map.Landforms, LandformKind.Mountain);
            Vector2Int plain = FindCell(map.Landforms, LandformKind.Plain);

            Assert.That(service.CanPlaceOffGrid(candle, new Vector3(mountain.x + .5f, 0f, mountain.y + .5f)).Failure,
                Is.EqualTo(BuildFailure.TerrainBlocked));
            BuildResult placed = service.TryPlaceOffGrid(
                candle, new Vector3(plain.x + .3f, 0f, plain.y + .7f), out BuildSite site);
            Assert.That(placed.Succeeded, Is.True, placed.Message);
            Assert.That(site.IsOffGrid, Is.True);
            Assert.That(site.OffGridCenter.Value, Is.EqualTo(new Vector2(plain.x + .3f, plain.y + .7f)));
        }

        /// <summary>
        /// Share of mountain cells, over a 300 m square, that belong to a sliver: along some axis or
        /// diagonal both cells four metres away are open ground, so the mountain there is under eight
        /// metres across. Broad massifs score near zero; wedges and tongues score high. Crater rims are
        /// thin rings by design and are left out.
        /// </summary>
        private static float SliverShare(LandformSampler sampler)
        {
            const int size = 300;
            const int reach = 4;
            var mountain = new bool[size, size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    mountain[x, z] = sampler.KindAt(x - 150f + .5f, z - 150f + .5f) == LandformKind.Mountain;
                }
            }

            int[,] directions = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };
            int mountainCells = 0;
            int slivers = 0;
            for (int z = reach; z < size - reach; z++)
            {
                for (int x = reach; x < size - reach; x++)
                {
                    if (!mountain[x, z] || IsNearCraterRim(sampler, x - 150f + .5f, z - 150f + .5f))
                    {
                        continue;
                    }

                    mountainCells++;
                    for (int d = 0; d < 4; d++)
                    {
                        int dx = directions[d, 0] * reach;
                        int dz = directions[d, 1] * reach;
                        if (!mountain[x + dx, z + dz] && !mountain[x - dx, z - dz])
                        {
                            slivers++;
                            break;
                        }
                    }
                }
            }

            Assert.That(mountainCells, Is.GreaterThan(500), "The sampled square holds too little mountain to judge.");
            return slivers / (float)mountainCells;
        }

        private static bool IsNearCraterRim(LandformSampler sampler, float x, float z)
        {
            const float regionSize = 170f;
            int regionX = Mathf.FloorToInt(x / regionSize);
            int regionZ = Mathf.FloorToInt(z / regionSize);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!sampler.Craters.TryGetCrater(regionX + dx, regionZ + dz, out Crater crater))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(new Vector2(x, z), new Vector2(crater.CenterX, crater.CenterZ));
                    if (distance > crater.Radius * .5f && distance < crater.Radius * 1.8f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Finds a cell of the given kind at least a crater's width from the landing site.</summary>
        private static Vector2Int FindCell(LandformSampler sampler, LandformKind kind)
        {
            for (int radius = 0; radius < 1500; radius += 3)
            {
                for (int step = 0; step < 32; step++)
                {
                    float angle = step * Mathf.PI / 16f;
                    int x = Mathf.RoundToInt(LandingSite.x + Mathf.Cos(angle) * radius);
                    int z = Mathf.RoundToInt(LandingSite.y + Mathf.Sin(angle) * radius);
                    if (sampler.KindAt(x + .5f, z + .5f) == kind)
                    {
                        return new Vector2Int(x, z);
                    }
                }
            }

            Assert.Fail($"No {kind} ground was found near the landing site.");
            return default;
        }

        private static Vector2Int FindLakeSquare(LandformSampler sampler, int size)
        {
            Vector2 lake = sampler.StarterLakeCenter;
            for (int z = -6; z <= 6; z++)
            {
                for (int x = -6; x <= 6; x++)
                {
                    var corner = new Vector2Int(Mathf.FloorToInt(lake.x) + x, Mathf.FloorToInt(lake.y) + z);
                    bool allLake = true;
                    for (int cz = 0; cz < size && allLake; cz++)
                    {
                        for (int cx = 0; cx < size && allLake; cx++)
                        {
                            allLake = sampler.KindAt(corner.x + cx + .5f, corner.y + cz + .5f) == LandformKind.IceLake;
                        }
                    }

                    if (allLake)
                    {
                        return corner;
                    }
                }
            }

            Assert.Fail("The starter lake has no room for a three-by-three structure.");
            return default;
        }

        private static int CountTrue(bool[] values)
        {
            int count = 0;
            foreach (bool value in values)
            {
                count += value ? 1 : 0;
            }

            return count;
        }

        private LandformSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<LandformSettings>();
            _created.Add(settings);
            return settings;
        }

        /// <summary>One-metre tiles so gameplay tiles line up with construction cells in these tests.</summary>
        private TerrainTileMap CreateMap(TerrainSurfaceDefinition coveringPatch, TerrainSurfaceDefinition iceLake)
        {
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            if (coveringPatch != null)
            {
                settings.Configure(0, 1f, 8, 1, new TerrainPatchLayer(coveringPatch, 20f, 1f, 0));
            }
            else
            {
                settings.Configure(0, 1f, 8, 1);
            }

            settings.ConfigureLandforms(CreateSettings(), iceLake);
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(WorldSeed, settings, 1f, LandingSite);
            return map;
        }

        private TerrainSurfaceDefinition CreateSurface(string terrainId)
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure(terrainId, terrainId, 2, 1f, string.Empty);
            _created.Add(surface);
            return surface;
        }

        private ItemDefinition CreateItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(id, id, 1, 20, false, true);
            _created.Add(item);
            return item;
        }

        private BuildableDefinition CreateBuildable(string id, ItemDefinition material, Vector2Int footprint)
        {
            var buildable = ScriptableObject.CreateInstance<BuildableDefinition>();
            buildable.Configure(id, id, footprint, 0f, new CraftingItemAmount(material, 1));
            _created.Add(buildable);
            return buildable;
        }
    }
}
