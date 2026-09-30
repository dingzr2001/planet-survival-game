using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.World.Ground;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class MountainRenderingTests
    {
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
        public void HeightProfile_IsBuriedOutside_ReachesItsFootHeightAtTheBrink_AndStaysBounded()
        {
            var appearance = new LandformAppearance();
            const float x = 13f;
            const float z = -7f;

            Assert.That(MountainHeightProfile.Height(-2f, 0f, x, z, appearance), Is.EqualTo(MountainHeightProfile.BuriedFoot));
            Assert.That(MountainHeightProfile.Height(0f, 0f, x, z, appearance), Is.EqualTo(MountainHeightProfile.BuriedFoot));

            float run = MountainHeightProfile.FootRunAt(x, z, appearance);
            float footHeight = MountainHeightProfile.FootHeightAt(x, z, appearance);
            Assert.That(MountainHeightProfile.Height(run, 0f, x, z, appearance), Is.GreaterThan(footHeight * .95f),
                "The foot should reach its full height at the brink.");
            for (float inside = 0f; inside < 60f; inside += .5f)
            {
                Assert.That(MountainHeightProfile.Height(inside, 5f, x, z, appearance),
                    Is.LessThanOrEqualTo(appearance.MaximumMountainHeight + 1e-4f));
            }
        }

        [Test]
        public void Foot_AlternatesBetweenRockCliffsAndScreeAlongAnEdge()
        {
            var appearance = new LandformAppearance();
            int cliffs = 0;
            int scree = 0;
            for (float x = 0f; x < 300f; x += 1f)
            {
                float run = MountainHeightProfile.FootRunAt(x, 40f, appearance);
                cliffs += run < 1f ? 1 : 0;
                scree += run > 2.5f ? 1 : 0;
            }

            Assert.That(cliffs, Is.GreaterThan(30), "Some stretches of edge should rise as rock cliffs.");
            Assert.That(scree, Is.GreaterThan(30), "Others should rise as scree slopes.");
        }

        [Test]
        public void HeightProfile_SwellsWithTheMassif_ButClimbsNoSteeperThanTheSlopeCapBehindTheBrink()
        {
            var appearance = new LandformAppearance();
            const float x = 41f;
            const float z = 17f;
            const float deep = 30f;

            float low = MountainHeightProfile.Height(deep, 0f, x, z, appearance);
            float high = MountainHeightProfile.Height(deep, .5f, x, z, appearance);
            Assert.That(high - low, Is.EqualTo(appearance.MassifRise * .5f).Within(.01f),
                "Deep inside, the massif should rise with the landform elevation above the mountain level.");

            // Right behind the brink a tall massif may not tower over the wall: the relief is capped by the
            // distance from the brink, whatever the elevation says.
            // Four metres in, the uncapped swell alone would add 4.8 m.
            const float inside = 4f;
            float tall = MountainHeightProfile.Height(inside, .8f, x, z, appearance);
            float flat = MountainHeightProfile.Height(inside, 0f, x, z, appearance);
            Assert.That(tall - flat, Is.LessThanOrEqualTo(inside * MountainHeightProfile.MaximumReliefSlope + 1e-4f));
        }

        [Test]
        public void HeightProfile_TopsVaryAcrossAMassif()
        {
            var appearance = new LandformAppearance();
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (float x = 0f; x < 60f; x += 1f)
            {
                float height = MountainHeightProfile.Height(30f, .3f, x, 5f, appearance);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }

            Assert.That(highest - lowest, Is.GreaterThan(appearance.RidgeHeight * .3f),
                "Crests and valleys should break up the top of an evenly high massif.");
        }

        [Test]
        public void MountainMesh_OnlyCoversGroundInsideTheBorder()
        {
            var appearance = new LandformAppearance();
            const int resolution = 16;
            const int gutter = 2;
            const float chunkSize = 16f;
            int size = resolution + 1 + gutter * 2;
            // A straight east-west border halfway up the chunk; mountain to the north.
            var inside = new float[size * size];
            var elevation = new float[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    inside[z * size + x] = (z - gutter) - resolution * .5f;
                    elevation[z * size + x] = 1f + inside[z * size + x] * .02f;
                }
            }

            var mesh = new Mesh();
            _created.Add(mesh);
            bool built = new MountainMeshBuilder().Build(mesh, inside, elevation, size, gutter, resolution,
                chunkSize, 0f, 0f, appearance);

            Assert.That(built, Is.True);
            Assert.That(mesh.bounds.min.z, Is.GreaterThanOrEqualTo(chunkSize * .5f - chunkSize / resolution - .01f),
                "No quad may lie wholly south of the border.");
            foreach (Vector3 normal in mesh.normals)
            {
                Assert.That(normal.y, Is.GreaterThan(-.01f), "Faces must point up and out towards the camera.");
            }
        }

        [Test]
        public void FallenRocks_LieOnlyInTheApronOutsideTheBorder_FarthestFirst()
        {
            var appearance = new LandformAppearance();
            const int resolution = 32;
            const int gutter = 2;
            const float chunkSize = 16f;
            int size = resolution + 1 + gutter * 2;
            float step = chunkSize / resolution;
            var inside = new float[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Border at z = 10 m; inside is north of it.
                    inside[z * size + x] = (z - gutter) * step - 10f;
                }
            }

            var mesh = new Mesh();
            _created.Add(mesh);
            var builder = new TalusMeshBuilder();
            bool built = builder.Build(mesh, inside, null, size, gutter, resolution, chunkSize, 0f, 0f, appearance, 99,
                4, 5);

            Assert.That(built, Is.True);
            Assert.That(builder.RockCount, Is.GreaterThan(3));
            Vector3[] vertices = mesh.vertices;
            List<Vector2> sizeAndSlice = new();
            mesh.GetUVs(1, sizeAndSlice);
            for (int i = 0; i < vertices.Length; i += 4)
            {
                float outside = 10f - vertices[i].z;
                Assert.That(outside, Is.InRange(.05f, appearance.TalusWidth * 1.25f + .01f));
                Assert.That(sizeAndSlice[i].y, Is.InRange(4f, 8f), "Rocks must use the rock slices only.");
                if (i >= 4)
                {
                    Assert.That(vertices[i].z, Is.LessThanOrEqualTo(vertices[i - 4].z), "Rocks are drawn far to near.");
                }
            }
        }

        [Test]
        public void EjectaRocks_LieOnlyWhereTheCraterMarkIsPositive_AndNeedMarks()
        {
            var appearance = new LandformAppearance();
            const int resolution = 32;
            const int gutter = 2;
            const float chunkSize = 16f;
            int size = resolution + 1 + gutter * 2;
            float step = chunkSize / resolution;
            var inside = new float[size * size];
            var marks = new float[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Open ground everywhere; ejecta in a band from z = 4 m to 8 m, crater floor south of it.
                    float worldZ = (z - gutter) * step;
                    inside[z * size + x] = -20f;
                    marks[z * size + x] = worldZ >= 4f && worldZ <= 8f ? 1f : worldZ < 4f ? -1f : 0f;
                }
            }

            var mesh = new Mesh();
            _created.Add(mesh);
            var builder = new TalusMeshBuilder();

            Assert.That(builder.Build(mesh, inside, null, size, gutter, resolution, chunkSize, 0f, 0f, appearance, 7,
                0, 5), Is.False, "Without crater marks open ground holds no rocks.");
            Assert.That(builder.Build(mesh, inside, marks, size, gutter, resolution, chunkSize, 0f, 0f, appearance, 7,
                0, 5), Is.True);
            Assert.That(builder.RockCount, Is.GreaterThan(5));
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i += 4)
            {
                // Bilinear filtering blurs the band edges by one texel.
                Assert.That(vertices[i].z, Is.InRange(4f - step, 8f + step), "Ejecta rocks must stay on the ejecta.");
            }
        }

        [Test]
        public void StandingMaterial_ReplacesTheDefaultSpriteMaterialButKeepsCustomOnes()
        {
            var plain = new GameObject("Plain");
            var custom = new GameObject("Custom");
            _created.Add(plain);
            _created.Add(custom);
            SpriteRenderer plainRenderer = plain.AddComponent<SpriteRenderer>();
            SpriteRenderer customRenderer = custom.AddComponent<SpriteRenderer>();
            var customMaterial = new Material(Shader.Find("Unlit/Transparent"));
            _created.Add(customMaterial);
            customRenderer.sharedMaterial = customMaterial;

            Assert.That(StandingSpriteMaterial.Shared, Is.Not.Null, "The shader must ship under Resources.");
            Assert.That(StandingSpriteMaterial.Apply(plainRenderer), Is.True);
            Assert.That(plainRenderer.sharedMaterial, Is.SameAs(StandingSpriteMaterial.Shared));
            Assert.That(StandingSpriteMaterial.Apply(customRenderer), Is.False);
            Assert.That(customRenderer.sharedMaterial, Is.SameAs(customMaterial));
        }
    }
}
