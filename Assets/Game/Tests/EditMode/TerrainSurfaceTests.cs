using NUnit.Framework;
using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Ground;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainSurfaceTests
    {
        private const int Seed = 4242;

        [Test]
        public void CraterProfile_SinksTheFloor_RaisesTheRim_AndLeavesThePlainAlone()
        {
            Assert.That(CraterRelief.Profile(0f), Is.EqualTo(-CraterRelief.Depth).Within(1e-4f));
            Assert.That(CraterRelief.Profile(CraterRelief.WallStart * .5f), Is.EqualTo(-CraterRelief.Depth).Within(1e-4f),
                "The floor is flat out to the foot of the wall.");
            Assert.That(CraterRelief.Profile(1f), Is.GreaterThan(0f), "The rim stands above the plain.");
            Assert.That(CraterRelief.Profile(CraterRelief.Reach), Is.Zero);
            Assert.That(Mathf.Abs(CraterRelief.Profile(CraterRelief.Reach - .01f)), Is.LessThan(1e-4f),
                "The ground must meet the plain without a step at the edge of the crater's reach.");
        }

        [Test]
        public void Surface_IsSunkIntoCraters_AtTheMountainsVerticalScale()
        {
            var settings = ScriptableObject.CreateInstance<LandformSettings>();
            try
            {
                settings.ConfigureCraters(1f, 0f);
                var craters = new CraterField(Seed, settings, new Vector2(-1e6f, -1e6f), 0f);
                var surface = new TerrainSurface(craters, 3f);
                Assert.That(craters.TryGetCrater(1, 1, out Crater crater), Is.True);

                Assert.That(surface.HeightAt(crater.CenterX, crater.CenterZ),
                    Is.EqualTo(-CraterRelief.Depth * crater.Radius * 3f).Within(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Surface_IsFlatWithoutCraters()
        {
            var settings = ScriptableObject.CreateInstance<LandformSettings>();
            try
            {
                settings.ConfigureCraters(0f, 0f);
                var surface = new TerrainSurface(new CraterField(Seed, settings, Vector2.zero, 0f), 3f);

                Assert.That(surface.HeightAt(37f, -12f), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Raycast_LandsWhereTheRayMeetsTheDrawnGround()
        {
            var bowl = new Bowl(new Vector2(10f, 20f), 15f, 12f);
            // The game camera: pitched 75° down, looking north.
            Vector3 direction = Quaternion.Euler(75f, 0f, 0f) * Vector3.forward;
            var ray = new Ray(new Vector3(8f, 60f, 20f) - direction * 40f, direction);

            Assert.That(GroundSurfaceRaycast.TryIntersect(bowl, ray, 0f, out Vector3 point), Is.True);
            Assert.That(point.y, Is.Zero, "The result is a gameplay position on the plane.");
            var drawn = new Vector3(point.x, bowl.HeightAt(point.x, point.z), point.z);
            float offRay = Vector3.Cross(ray.direction, drawn - ray.origin).magnitude;
            Assert.That(offRay, Is.LessThan(.01f), "The drawn ground under the result must lie on the ray.");
        }

        [Test]
        public void Raycast_OnFlatGround_IsThePlaneIntersection()
        {
            var ray = new Ray(new Vector3(3f, 10f, -4f), new Vector3(0f, -1f, 1f).normalized);

            Assert.That(GroundSurfaceRaycast.TryIntersect(null, ray, 0f, out Vector3 point), Is.True);
            Assert.That(Vector3.Distance(point, new Vector3(3f, 0f, 6f)), Is.LessThan(1e-4f));
            Assert.That(GroundSurfaceRaycast.ToGameplaySpace(null, ray, 0f).origin, Is.EqualTo(ray.origin));
        }

        /// <summary>A smooth round dip, as deep and steep as a large crater drawn at the mountains' scale.</summary>
        private sealed class Bowl : IGroundSurface
        {
            private readonly Vector2 _centre;
            private readonly float _radius;
            private readonly float _depth;

            public Bowl(Vector2 centre, float radius, float depth)
            {
                _centre = centre;
                _radius = radius;
                _depth = depth;
            }

            public float HeightAt(float x, float z)
            {
                float relative = Vector2.Distance(new Vector2(x, z), _centre) / _radius;
                float t = Mathf.Clamp01(relative);
                return -_depth * (1f - t * t * (3f - 2f * t));
            }
        }
    }
}
